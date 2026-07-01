using QingLi.Infrastructure.ClockReplacement;
using QingLi.Windows.ClockReplacement;

if (args.Length != 1 || !string.Equals(args[0], "--restore-clock", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Usage: QingLi.Recovery.exe --restore-clock");
    return 2;
}

var recovery = new ClockRecoveryService(
    new WindowsSystemClockPolicy(),
    new SystemClockStateStore());
var result = await recovery.RestoreAsync(CancellationToken.None);
Console.WriteLine(result.Message);
return result.Succeeded ? 0 : 1;
