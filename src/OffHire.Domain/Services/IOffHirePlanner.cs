using OffHire.Domain.Models;

namespace OffHire.Domain.Services;

public interface IOffHirePlanner
{
    OffHirePlanningResult Plan(OffHirePlanningContext context);
}

