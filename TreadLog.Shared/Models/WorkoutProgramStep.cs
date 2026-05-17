namespace TreadLog.Models;

public class WorkoutProgramStep
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public int StepOrder { get; set; }
    public int DurationSeconds { get; set; }
    public double SpeedKmh { get; set; }
}
