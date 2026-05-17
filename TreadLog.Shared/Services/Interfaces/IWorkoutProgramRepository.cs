using TreadLog.Models;

namespace TreadLog.Services.Interfaces;

public interface IWorkoutProgramRepository
{
    Task<IEnumerable<WorkoutProgram>> GetAllAsync();
    Task<WorkoutProgram?> GetByIdAsync(int id);
    Task<int> AddAsync(WorkoutProgram program);
    Task UpdateAsync(WorkoutProgram program);
    Task DeleteAsync(int id);
}
