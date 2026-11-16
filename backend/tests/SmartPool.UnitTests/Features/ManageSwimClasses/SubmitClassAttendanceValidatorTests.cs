using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class SubmitClassAttendanceValidatorTests
{
    private readonly SubmitClassAttendanceValidator _validator = new();
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xD9];

    [Fact(DisplayName = "UTCID01 (N) - ClassId, SessionId và ảnh JPG hợp lệ → không có lỗi")]
    public void Validate_ValidJpeg_IsValid() =>
        Assert.True(_validator.Validate(Command(File(Jpeg, "image/jpeg"))).IsValid);

    [Fact(DisplayName = "UTCID02 (A) - ClassId rỗng → lỗi ClassId")]
    public void Validate_EmptyClassId_HasError()
    {
        var command = Command(File(Jpeg, "image/jpeg"));
        command.ClassId = Guid.Empty;
        AssertError(command, "ClassId");
    }

    [Fact(DisplayName = "UTCID03 (A) - SessionId rỗng → lỗi SessionId")]
    public void Validate_EmptySessionId_HasError()
    {
        var command = Command(File(Jpeg, "image/jpeg"));
        command.SessionId = Guid.Empty;
        AssertError(command, "SessionId");
    }

    [Fact(DisplayName = "UTCID04 (A) - Không chọn ảnh → lỗi Image")]
    public void Validate_NoImage_HasError() =>
        AssertError(Command(null), "Image", "Vui lòng chọn ảnh điểm danh.");

    [Fact(DisplayName = "UTCID05 (B) - Ảnh dưới 4 byte → lỗi Image")]
    public void Validate_TooSmall_HasError() =>
        AssertError(Command(File([1, 2, 3], "image/jpeg")), "Image", "Chỉ nhận ảnh JPG/PNG tối đa 5 MB.");

    [Fact(DisplayName = "UTCID06 (B) - Ảnh trên 5 MB → lỗi Image")]
    public void Validate_TooLarge_HasError() =>
        AssertError(Command(File(new byte[SubmitClassAttendanceValidator.MaxImageBytes + 1], "image/png")),
            "Image", "Chỉ nhận ảnh JPG/PNG tối đa 5 MB.");

    [Fact(DisplayName = "UTCID07 (A) - MIME khác JPG/PNG → lỗi Image")]
    public void Validate_UnsupportedMime_HasError() =>
        AssertError(Command(File(Jpeg, "image/gif")), "Image", "Chỉ nhận ảnh JPG/PNG tối đa 5 MB.");

    private void AssertError(SubmitClassAttendanceCommand command, string property, string? message = null)
    {
        ValidationResult result = _validator.Validate(command);
        Assert.Contains(result.Errors, x => x.PropertyName == property &&
            (message is null || x.ErrorMessage == message));
    }

    private static SubmitClassAttendanceCommand Command(IFormFile? image) => new()
    {
        ClassId = Guid.NewGuid(), SessionId = Guid.NewGuid(), Image = image
    };

    private static IFormFile File(byte[] bytes, string contentType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "attendance.jpg")
        { Headers = new HeaderDictionary(), ContentType = contentType };
}
