namespace SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;

public static class SwimClassUpdatePolicy
{
    public static void EnsureCapacityAndFee(
        int requestedCapacity,
        int occupiedPlaces,
        decimal currentFee,
        decimal requestedFee,
        bool hasRegistration)
    {
        if (requestedCapacity < occupiedPlaces)
            throw new SwimClassStateConflictException(
                $"Sức chứa không được nhỏ hơn {occupiedPlaces} chỗ đã xác nhận hoặc đang chờ thanh toán.");

        if (requestedFee != currentFee && hasRegistration)
            throw new SwimClassStateConflictException(
                "Không thể thay đổi học phí khi lớp đã có đăng ký hoặc order liên quan.");
    }
}
