using System.Globalization;
using System.ServiceProcess;
using CsvHelper;
using CsvHelper.Configuration;
using Timer = System.Timers.Timer;

namespace DSTService;

public class TimeZoneService : ServiceBase
{
    private Timer _timer;
    private readonly string _filePath;

    public TimeZoneService(string filePath)
    {
        ServiceName = "TimeZoneDSTService";
        _filePath = filePath;
    }

    protected override void OnStart(string[] args)
    {
        _timer = new Timer(86400000);
        _timer.Elapsed += (sender, eventArgs) => CheckAndUpdateTimeZones();
        _timer.Start();
    }

    private void CheckAndUpdateTimeZones()
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        List<TimeZoneRecord> records;

        using (var streamReader = new StreamReader(_filePath))
        {
            using var csvReader = new CsvReader(streamReader, config);
            records = csvReader.GetRecords<TimeZoneRecord>().ToList();
        }

        var updatedRecords = new List<TimeZoneRecord>();

        foreach (var record in records)
        {
            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(record.TimeZoneId);
            bool isDstEnabled = timeZoneInfo.SupportsDaylightSavingTime;
            record.IsDSTEnabled = isDstEnabled ? 1 : 0;

            if (isDstEnabled)
            {
                var nextTransition = GetNextTransition(timeZoneInfo);
                record.DSTLastUpdate = DateTime.UtcNow.ToString("o");
                record.DSTNextUpdate = nextTransition?.ToString("o") ?? "N/A";
            }

            updatedRecords.Add(record);
        }

        using (var streamWriter = new StreamWriter(_filePath))
        {
            using var csvWriter = new CsvWriter(streamWriter, config);
            csvWriter.WriteRecords(updatedRecords);
        }
    }

    private DateTime? GetNextTransition(TimeZoneInfo tz)
    {
        DateTime now = DateTime.UtcNow;
        var rules = tz.GetAdjustmentRules();

        DateTime? nextTransition = null;

        foreach (var rule in rules)
        {
            if (rule.DateEnd < now)
                continue;

            DateTime thisYearStart = TransitionTimeToDateTime(now.Year, rule.DaylightTransitionStart);
            DateTime thisYearEnd = TransitionTimeToDateTime(now.Year, rule.DaylightTransitionEnd);

            if (now < thisYearStart)
            {
                nextTransition = thisYearStart;
                break;
            }

            if (now < thisYearEnd)
            {
                nextTransition = thisYearEnd;
                break;
            }

            if (now.Year < rule.DateEnd.Year)
            {
                DateTime nextYearStart = TransitionTimeToDateTime(now.Year + 1, rule.DaylightTransitionStart);
                nextTransition = nextYearStart;
                break;
            }
        }

        return nextTransition;
    }

    private DateTime TransitionTimeToDateTime(int year, TimeZoneInfo.TransitionTime transitionTime)
    {
        if (transitionTime.IsFixedDateRule)
        {
            int daysInMonth = DateTime.DaysInMonth(year, transitionTime.Month);
            int day = Math.Min(transitionTime.Day, daysInMonth);
            return new DateTime(year, transitionTime.Month, day, transitionTime.TimeOfDay.Hour,
                transitionTime.TimeOfDay.Minute, transitionTime.TimeOfDay.Second);
        }
        else
        {
            var firstDayOfMonth = new DateTime(year, transitionTime.Month, 1);
            var changeDayOfWeek = transitionTime.DayOfWeek;

            int currentDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
            int wantedDayOfWeek = (int)changeDayOfWeek;

            int dayShift = (wantedDayOfWeek - currentDayOfWeek + 7) % 7;
            int firstChangeDay = firstDayOfMonth.Day + dayShift;

            int resultingDay = firstChangeDay + (transitionTime.Week - 1) * 7;

            DateTime tempDate = new DateTime(year, transitionTime.Month, 1, transitionTime.TimeOfDay.Hour,
                transitionTime.TimeOfDay.Minute, transitionTime.TimeOfDay.Second);
            tempDate = tempDate.AddDays(resultingDay - 1);

            if (tempDate.Month != transitionTime.Month)
            {
                tempDate = tempDate.AddDays(-7);
            }

            return tempDate;
        }
    }

    protected override void OnStop()
    {
        _timer.Stop();
    }


    static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            if (args[0] == "--help")
            {
                Console.WriteLine("Usage: DSTService.exe <path_to_csv>");
                return;
            }

            if (File.Exists(args[0]) && Path.GetExtension(args[0]).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                Run(new TimeZoneService(args[0]));
            }
            else
            {
                Console.WriteLine("Invalid file path or file extension.");
            }
        }
        else
        {
            Console.WriteLine("Usage: DSTService.exe <path_to_csv>");
        }
    }
}