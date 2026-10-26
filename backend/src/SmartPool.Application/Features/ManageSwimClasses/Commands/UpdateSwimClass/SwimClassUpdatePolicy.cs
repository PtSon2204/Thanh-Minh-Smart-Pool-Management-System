namespace SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;

public static class SwimClassUpdatePolicy
{
    public static void EnsureCapacityAndFee(
        int requestedCapacity,
        int confirmedStudents,
        decimal currentFee,
        decimal requestedFee,
        bool hasRegistration)
    {
        if (requestedCapacity < confirmedStudents)
            throw new SwimClassStateConflictException(
                $"Sức chứa không được nhỏ hơn {confirmedStudents} học viên đã xác nhận.");

        if (requestedFee != currentFee && hasRegistration)
            throw new SwimClassStateConflictException(
                "Không thể thay đổi học phí khi lớp đã có đăng ký hoặc order liên quan.");
    }
}
