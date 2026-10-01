using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageStaffs.Commands.SaveSalary
{
    public sealed class SaveSalaryCommand : IRequest<SaveSalaryResponse>
    {
        [JsonIgnore]
        public Guid UserId { get; set; }
        [JsonIgnore]
        public Guid? Id { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public int TotalShifts { get; set; }
        public decimal Bonus { get; set; }
        public decimal Deduction { get; set; }
        public decimal NetSalary { get; set; }
    }
}
