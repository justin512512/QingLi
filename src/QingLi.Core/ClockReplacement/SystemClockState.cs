using System.Text.Json.Serialization;

namespace QingLi.Core.ClockReplacement;

public sealed record SystemClockState(
    [property: JsonRequired] bool ValueExisted,
    [property: JsonRequired] int? OriginalValue,
    [property: JsonRequired] DateTimeOffset CapturedAt);
