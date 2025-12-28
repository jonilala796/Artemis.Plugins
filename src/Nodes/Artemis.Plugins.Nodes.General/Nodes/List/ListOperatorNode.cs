using System.Collections;
using Artemis.Core;
using Artemis.Plugins.Nodes.General.Nodes.List.Screens;

namespace Artemis.Plugins.Nodes.General.Nodes.List;

[Node("List Operator (Simple)", "Checks if any/all/no values in the input list match the input value. If the input pin is left empty it just checks whether there are any/no values.", "List", InputType = typeof(IEnumerable), OutputType = typeof(bool))]
public class ListOperatorNode : Node<ListOperator, ListOperatorNodeCustomViewModel>
{
    public ListOperatorNode()
    {
        InputList = CreateInputPin<IList>();
        InputValue = CreateInputPin<object>();

        Output = CreateOutputPin(typeof(object));
        StorageModified += OnStorageModified;
    }

    public InputPin<IList> InputList { get; }
    public InputPin<object> InputValue { get; }
    public OutputPin Output { get; }

    public override void Initialize(INodeScript script)
    {
        UpdateOutputType();
    }

    /// <inheritdoc />
    public override void Evaluate()
    {
        if (InputList.Value == null)
        {
            Output.Value = Storage == ListOperator.None;
            return;
        }

        IEnumerable<object> items = InputList.Value.Cast<object>();
        bool hasInputValue = InputValue.ConnectedTo.Count > 0;
        
        Output.Value = Storage switch
        {
            ListOperator.Any => hasInputValue 
                ? items.Any(v => v.Equals(InputValue.Value))
                : items.Any(),
            ListOperator.All => hasInputValue 
                ? items.All(v => v.Equals(InputValue.Value))
                : items.Any(), // Doesn't really make sense without an input value
            ListOperator.None => hasInputValue 
                ? items.All(v => !v.Equals(InputValue.Value))
                : !items.Any(),
            ListOperator.Count => hasInputValue 
                ? (Numeric)items.Count(v => v.Equals(InputValue.Value))
                : (Numeric)items.Count(),
            _ => false
        };
    }

    private void UpdateOutputType()
    {
        if (Storage is ListOperator.All or ListOperator.Any or ListOperator.None)
            Output.ChangeType(typeof(bool));
        else
            Output.ChangeType(typeof(Numeric));
    }
    
    private void OnStorageModified(object? sender, EventArgs e)
    {
        UpdateOutputType();
    }
}

public enum ListOperator
{
    Any,
    All,
    None,
    Count
}