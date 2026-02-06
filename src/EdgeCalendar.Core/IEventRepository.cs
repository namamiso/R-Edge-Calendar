using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EdgeCalendar.Core
{
    public interface IEventRepository
    {
        Task InitializeAsync();
        Task<IReadOnlyList<EventItem>> GetByDateAsync(DateTime dateLocal);
        Task<long> CreateAsync(EventItem item);
        Task UpdateAsync(EventItem item);
        Task DeleteAsync(long id);
        Task ReplaceExternalEventsAsync(string calendarId, DateTime windowStart, DateTime windowEnd, IReadOnlyList<EventItem> items);
    }
}
