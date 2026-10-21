using System.Security.Cryptography;
using System.Text;

namespace SmartPool.Application.Features.ManageStaffs.AttendanceQr;

public sealed class StaffAttendanceQrConflictException(string message) : Exception(message);
