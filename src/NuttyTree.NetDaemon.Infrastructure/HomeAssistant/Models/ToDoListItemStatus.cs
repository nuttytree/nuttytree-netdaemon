using System.Diagnostics.CodeAnalysis;

namespace NuttyTree.NetDaemon.Infrastructure.HomeAssistant.Models;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Name must match Home Assistant")]
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Name must match Home Assistant")]
public enum ToDoListItemStatus
{
    needs_action,
    completed
}
