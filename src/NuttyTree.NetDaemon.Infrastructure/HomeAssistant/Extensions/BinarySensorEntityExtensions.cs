namespace NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
public static class BinarySensorEntityExtensions
{
    public static bool IsOn(this BinarySensorEntity binarySensor) => binarySensor.State.CaseInsensitiveEquals("on");

    public static bool IsOff(this BinarySensorEntity binarySensor) => !binarySensor.IsOn();
}
