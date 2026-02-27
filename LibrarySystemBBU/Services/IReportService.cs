using System.Threading.Tasks;
using LibrarySystemBBU.ViewModels;

namespace LibrarySystemBBU.Services
{
    public interface IReportService
    {
        Task<MonthlyReportViewModel> GetMonthlyReportAsync(int year, int month);
    }
}
