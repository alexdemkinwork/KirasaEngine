namespace KirasaEngine.Core.Infrastructure.Services;
[RegisterSingleton]
public class TimeService
{
    public long DeltaTimeTicks { get; private set; }
    public long PrewiousTimeTicks { get; private set; }
    public List<Timer> Timers { get; set; } = new();
    public void UpdateDeltaTime() => DeltaTimeTicks = DateTime.Now.Ticks - PrewiousTimeTicks; 
    public void ReadTime() => PrewiousTimeTicks = DateTime.Now.Ticks;
    public float GetDeltaTimeSeconds() => (float)DeltaTimeTicks / TimeSpan.TicksPerSecond;
}