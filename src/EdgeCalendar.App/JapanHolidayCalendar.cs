using System;
using System.Collections.Generic;

namespace EdgeCalendar.App
{
    internal static class JapanHolidayCalendar
    {
        public static IReadOnlyDictionary<DateTime, string> GetHolidays(int year)
        {
            var holidays = new SortedDictionary<DateTime, string>();

            Add(holidays, new DateTime(year, 1, 1), "元日");
            Add(holidays, NthMonday(year, 1, 2), "成人の日");
            Add(holidays, new DateTime(year, 2, 11), "建国記念の日");
            Add(holidays, new DateTime(year, 2, 23), "天皇誕生日");
            Add(holidays, new DateTime(year, 3, VernalEquinoxDay(year)), "春分の日");
            Add(holidays, new DateTime(year, 4, 29), "昭和の日");
            Add(holidays, new DateTime(year, 5, 3), "憲法記念日");
            Add(holidays, new DateTime(year, 5, 4), "みどりの日");
            Add(holidays, new DateTime(year, 5, 5), "こどもの日");
            Add(holidays, NthMonday(year, 7, 3), "海の日");
            Add(holidays, new DateTime(year, 8, 11), "山の日");
            Add(holidays, NthMonday(year, 9, 3), "敬老の日");
            Add(holidays, new DateTime(year, 9, AutumnalEquinoxDay(year)), "秋分の日");
            Add(holidays, NthMonday(year, 10, 2), "スポーツの日");
            Add(holidays, new DateTime(year, 11, 3), "文化の日");
            Add(holidays, new DateTime(year, 11, 23), "勤労感謝の日");

            AddSubstituteHolidays(holidays);
            AddCitizensHolidays(holidays, year);

            return holidays;
        }

        private static void Add(IDictionary<DateTime, string> holidays, DateTime date, string name)
        {
            holidays[date.Date] = name;
        }

        private static DateTime NthMonday(int year, int month, int nth)
        {
            var date = new DateTime(year, month, 1);
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)date.DayOfWeek + 7) % 7;
            return date.AddDays(daysUntilMonday + ((nth - 1) * 7));
        }

        private static int VernalEquinoxDay(int year)
        {
            return (int)Math.Floor(20.8431 + (0.242194 * (year - 1980)) - Math.Floor((year - 1980) / 4.0));
        }

        private static int AutumnalEquinoxDay(int year)
        {
            return (int)Math.Floor(23.2488 + (0.242194 * (year - 1980)) - Math.Floor((year - 1980) / 4.0));
        }

        private static void AddSubstituteHolidays(SortedDictionary<DateTime, string> holidays)
        {
            var originalDates = new List<DateTime>(holidays.Keys);
            foreach (var date in originalDates)
            {
                if (date.DayOfWeek != DayOfWeek.Sunday)
                {
                    continue;
                }

                var substitute = date.AddDays(1);
                while (holidays.ContainsKey(substitute))
                {
                    substitute = substitute.AddDays(1);
                }

                holidays[substitute] = "振替休日";
            }
        }

        private static void AddCitizensHolidays(SortedDictionary<DateTime, string> holidays, int year)
        {
            var date = new DateTime(year, 1, 2);
            var end = new DateTime(year, 12, 30);

            while (date <= end)
            {
                if (date.DayOfWeek != DayOfWeek.Sunday &&
                    !holidays.ContainsKey(date) &&
                    holidays.ContainsKey(date.AddDays(-1)) &&
                    holidays.ContainsKey(date.AddDays(1)))
                {
                    holidays[date] = "国民の休日";
                }

                date = date.AddDays(1);
            }
        }
    }
}
