using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace AIEnablement.Infrastructure.Plugins;

public class DateToolsPlugin
{
    public DateToolsPlugin()
    {

    }

    [KernelFunction("get_current_date")]
    [Description("Gets the current system date. Use this when the user asks about today's date, what day it is, or the current date.")]
    public string GetCurrentDate()
    {
        var now = DateTime.Now;

        var result = now.ToString("MMMM d, yyyy (dddd)");

        return result;
    }

    [KernelFunction("add_days_to_date")]
    [Description("Adds or subtracts days from a given date. Use this to calculate future or past dates. Accepts positive numbers for future dates and negative numbers for past dates.")]
    public string AddDaysToDate(
        [Description("The starting date in format 'yyyy-MM-dd' or 'MMMM d, yyyy'. If not provided, use today's date.")]
        string startDate,
        [Description("Number of days to add (positive) or subtract (negative)")]
        int days)
    {
        DateTime date;

        // Try to parse the input date
        if (string.IsNullOrWhiteSpace(startDate) || startDate.Equals("today", StringComparison.CurrentCultureIgnoreCase))
        {
            date = DateTime.Now;
        }
        else if (!DateTime.TryParse(startDate, out date))
        {
            return $"Error: Could not parse date '{startDate}'. Please use format like 'yyyy-MM-dd' or 'MMMM d, yyyy'.";
        }

        var resultDate = date.AddDays(days);
        var result = resultDate.ToString("MMMM d, yyyy (dddd)");

        return result;
    }
}
