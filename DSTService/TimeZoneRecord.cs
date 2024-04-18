using CsvHelper.Configuration.Attributes;

namespace DSTService;

public class TimeZoneRecord
{
    [Index(0)]
    public string TimeZoneId { get; set; }

    [Index(1)]
    public string DisplayName { get; set; }
    
    [Index(2)]
    public string StandardName { get; set; }
    
    [Index(3)]
    public string UTCOffsetHour { get; set; }
    
    [Index(4)]
    public int IsDSTEnabled { get; set; }

    [Index(5)]
    public string DSTLastUpdate { get; set; }

    [Index(6)]
    public string DSTNextUpdate { get; set; }
}