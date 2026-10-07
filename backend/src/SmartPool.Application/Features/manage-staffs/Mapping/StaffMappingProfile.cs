using AutoMapper;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;
using SmartPool.Application.Features.ManageStaffs.Commands.CancelShiftAssignment;
using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Features.ManageStaffs.Commands.SaveSalary;
using SmartPool.Application.Features.ManageStaffs.Commands.SaveShift;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSalaries;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;
using SmartPool.Application.Features.ManageStaffs.Queries.GetShifts;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffOptions;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffs;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageStaffs.Mapping
{
    public sealed class StaffMappingProfile : Profile
    {
        public StaffMappingProfile()
        {
            CreateMap<Employee, GetStaffsResponse>()
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.User.Username))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.UserProfile == null ? string.Empty : src.User.UserProfile.FullName))
                .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.User.UserProfile == null ? null : src.User.UserProfile.AvatarUrl))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.User.UserProfile == null ? null : src.User.UserProfile.Address))
                .ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.User.UserProfile == null ? null : src.User.UserProfile.DateOfBirth))
                .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.User.RoleId ?? Guid.Empty))
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.User.Role == null ? string.Empty : src.User.Role.Name));
            CreateMap<Employee, CreateStaffResponse>()
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.User.Username))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.UserProfile == null ? string.Empty : src.User.UserProfile.FullName))
                .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.User.UserProfile == null ? null : src.User.UserProfile.AvatarUrl))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.User.UserProfile == null ? null : src.User.UserProfile.Address))
                .ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.User.UserProfile == null ? null : src.User.UserProfile.DateOfBirth))
                .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.User.RoleId ?? Guid.Empty))
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.User.Role == null ? string.Empty : src.User.Role.Name));
            CreateMap<Employee, UpdateStaffResponse>()
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.User.Username))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.UserProfile == null ? string.Empty : src.User.UserProfile.FullName))
                .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.User.UserProfile == null ? null : src.User.UserProfile.AvatarUrl))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.User.UserProfile == null ? null : src.User.UserProfile.Address))
                .ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.User.UserProfile == null ? null : src.User.UserProfile.DateOfBirth))
                .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.User.RoleId ?? Guid.Empty))
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.User.Role == null ? string.Empty : src.User.Role.Name));
            CreateMap<Shift, GetShiftsResponse>().ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive ?? false));
            CreateMap<Shift, SaveShiftResponse>().ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive ?? false));
            CreateMap<User, EligibleUserResponse>().ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id)).ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.UserProfile == null ? null : src.UserProfile.FullName));
            CreateMap<EmployeeShift, GetScheduleResponse>().ForMember(dest => dest.ShiftId, opt => opt.MapFrom(src => src.ShiftId)).ForMember(dest => dest.ShiftName, opt => opt.MapFrom(src => src.Shift.Name)).ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.Shift.StartTime)).ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.Shift.EndTime)).ForMember(dest => dest.IsShiftActive, opt => opt.MapFrom(src => src.Shift.IsActive ?? false));
            CreateMap<EmployeeShift, AssignShiftResponse>().ForMember(dest => dest.ShiftName, opt => opt.MapFrom(src => src.Shift.Name)).ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.Shift.StartTime)).ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.Shift.EndTime)).ForMember(dest => dest.IsShiftActive, opt => opt.MapFrom(src => src.Shift.IsActive ?? false));
            CreateMap<EmployeeShift, CancelShiftAssignmentResponse>().ForMember(dest => dest.ShiftName, opt => opt.MapFrom(src => src.Shift.Name)).ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.Shift.StartTime)).ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.Shift.EndTime)).ForMember(dest => dest.IsShiftActive, opt => opt.MapFrom(src => src.Shift.IsActive ?? false));
            CreateMap<EmployeeShift, RecordAttendanceResponse>().ForMember(dest => dest.ShiftName, opt => opt.MapFrom(src => src.Shift.Name)).ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.Shift.StartTime)).ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.Shift.EndTime)).ForMember(dest => dest.IsShiftActive, opt => opt.MapFrom(src => src.Shift.IsActive ?? false));
            CreateMap<Salary, GetSalariesResponse>();
            CreateMap<Salary, SaveSalaryResponse>();
        }
    }
}
