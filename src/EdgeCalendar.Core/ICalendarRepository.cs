using System.Collections.Generic;
using System.Threading.Tasks;

namespace EdgeCalendar.Core
{
    public interface ICalendarRepository
    {
        Task InitializeAsync();
        Task<IReadOnlyList<CalendarInfo>> GetAllAsync();
        Task UpsertAsync(IReadOnlyList<CalendarInfo> calendars);
        Task UpdateSelectionAsync(IReadOnlyList<CalendarInfo> calendars);
    }
}
