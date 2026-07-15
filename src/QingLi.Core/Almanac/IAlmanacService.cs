namespace QingLi.Core.Almanac;

public interface IAlmanacService
{
    AlmanacDay GetDay(DateOnly date);
}
