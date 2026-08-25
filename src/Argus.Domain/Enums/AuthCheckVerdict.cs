namespace Argus.Domain.Enums;

public enum AuthCheckVerdict
{
    Unknown = 0,
    Pass = 1,
    Fail = 2,
    SoftFail = 3,
    Neutral = 4,
    None = 5,
    Missing = 6
}