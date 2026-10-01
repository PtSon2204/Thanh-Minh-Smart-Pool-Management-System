namespace SmartPool.Application.Features.ManageStaffs.Commands.SaveSalary
{
    public sealed class SaveSalaryResponse
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public int TotalShifts { get; set; }
        public decimal Bonus { get; set; }
        public decimal Deduction { get; set; }
        public decimal NetSalary { get; set; }
    }
}
