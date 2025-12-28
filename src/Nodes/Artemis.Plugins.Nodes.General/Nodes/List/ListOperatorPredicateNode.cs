using System.Collections;
using Artemis.Core;
using Artemis.Core.Events;
using Artemis.Plugins.Nodes.General.Nodes.List.Screens;

namespace Artemis.Plugins.Nodes.General.Nodes.List;

[Node("List Operator (Advanced)", "Checks if any/all/no values in the input list match a condition", "List", InputType = typeof(IEnumerable), OutputType = typeof(bool))]
public class ListOperatorPredicateNode : Node<ListOperatorEntity, ListOperatorPredicateNodeCustomViewModel>, IDisposable
{
    private readonly object _scriptLock = new();
    private readonly ListOperatorPredicateStartNode _startNode;

    public ListOperatorPredicateNode()
    {
        _startNode = new ListOperatorPredicateStartNode {X = -200};

        InputList = CreateInputPin<IList>();
        InputList.PinConnected += InputListOnPinConnected;

        Output = CreateOutputPin(typeof(object));
        StorageModified += OnStorageModified;
    }

    public InputPin<IList> InputList { get; }
    public OutputPin Output { get; }
    public NodeScript<bool>? Script { get; private set; }
    public bool EditorOpen { get; set; }

    public override void Initialize(INodeScript script)
    {
        Storage ??= new ListOperatorEntity();
        UpdateOutputType();
        
        lock (_scriptLock)
        {
            Script = Storage?.Script != null
                ? new NodeScript<bool>("Is match", "Determines whether the current list item is a match", Storage.Script, script.Context, new List<DefaultNode> {_startNode})
                : new NodeScript<bool>("Is match", "Determines whether the current list item is a match", script.Context, new List<DefaultNode> {_startNode});
        }
    }

    /// <inheritdoc />
    public override void Evaluate()
    {
        if (Storage == null || EditorOpen)
            return;

        if (InputList.Value == null)
        {
            Output.Value = Storage.Operator == ListOperator.None;
            return;
        }

        lock (_scriptLock)
        {
            if (Script == null)
                return;

            if (Storage.Operator == ListOperator.Any)
                Output.Value = InputList.Value.Cast<object>().Any(EvaluateItem);
            else if (Storage.Operator == ListOperator.All)
                Output.Value = InputList.Value.Cast<object>().All(EvaluateItem);
            else if (Storage.Operator == ListOperator.None)
                Output.Value = InputList.Value.Cast<object>().All(v => !EvaluateItem(v));
            else if (Storage.Operator == ListOperator.Count)
                Output.Value = (Numeric) InputList.Value.Cast<object>().Count(EvaluateItem);
        }
    }

    public override IEnumerable<PluginFeature> GetFeatureDependencies()
    {
        return [..base.GetFeatureDependencies(), ..Script?.GetFeatureDependencies() ?? []];
    }

    private bool EvaluateItem(object item)
    {
        if (Script == null)
            return false;

        _startNode.Item = item;
        Script.Run();
        return Script.Result;
    }

    private void UpdateStartNode()
    {
        Type? type = InputList.ConnectedTo.FirstOrDefault()?.Type;
        if (type == null)
            return;

        Type? elementType = GetCollectionElementType(type);
        if (elementType != null)
            _startNode.ChangeType(elementType);
    }

    private static Type? GetCollectionElementType(Type collectionType)
    {
        // Handle arrays first (e.g., int[], string[])
        if (collectionType.IsArray)
            return collectionType.GetElementType();

        // Handle generic collections (e.g., List<T>, IEnumerable<T>)
        if (collectionType.IsGenericType)
        {
            // Get the generic type definition (e.g., List<> from List<string>)
            Type genericTypeDef = collectionType.GetGenericTypeDefinition();
            
            // Check if it's a collection-like generic type
            if (typeof(IEnumerable<>).IsAssignableFrom(genericTypeDef) || collectionType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                return collectionType.GetGenericArguments().FirstOrDefault();
        }

        // Handle non-generic IEnumerable implementations by checking implemented interfaces
        Type? enumerableInterface = collectionType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerableInterface?.GetGenericArguments().FirstOrDefault();
    }

    private void OnStorageModified(object? sender, EventArgs e)
    {
        UpdateOutputType();
    }

    private void UpdateOutputType()
    {
        if (Storage == null)
            return;

        if (Storage.Operator is ListOperator.All or ListOperator.Any or ListOperator.None)
            Output.ChangeType(typeof(bool));
        else
            Output.ChangeType(typeof(Numeric));
    }

    private void InputListOnPinConnected(object? sender, SingleValueEventArgs<IPin> e)
    {
        lock (_scriptLock)
        {
            UpdateStartNode();
            Script?.LoadConnections();
        }
    }

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        Script?.Dispose();
        Script = null;
    }

    #endregion
}