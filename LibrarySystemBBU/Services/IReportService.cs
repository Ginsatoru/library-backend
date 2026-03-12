using System;
using System.Threading.Tasks;
using LibrarySystemBBU.ViewModels;

namespace LibrarySystemBBU.Services
{
    public interface IReportService
    {
        Task<MonthlyReportViewModel> GetReportAsync(DateTime start, DateTime end);
    }
}