namespace NuttyTree.NetDaemon.Application.Frigate.Models;

internal record CreateEventRequest(
    string camera,
    string event_name,
    string event_id_entity);
