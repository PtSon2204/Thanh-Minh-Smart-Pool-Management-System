import { useMutation, useQueryClient } from "@tanstack/react-query";
import staffService from "../services/staffService";
import { STAFFS_QUERY_KEY } from "./useStaffs";

function useStaffMutation(mutationFn) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: STAFFS_QUERY_KEY });
      queryClient.invalidateQueries({ queryKey: ["staff-options"] });
      queryClient.invalidateQueries({ queryKey: ["staff-schedule-employees"] });
    },
  });
}

export function useCreateStaff() {
  return useStaffMutation(staffService.create);
}

export function useUpdateStaff() {
  return useStaffMutation(({ userId, data }) =>
    staffService.update(userId, data),
  );
}

export function useResetStaffPassword() {
  return useStaffMutation(({ userId, personalEmail }) =>
    staffService.resetPassword(userId, { personalEmail }),
  );
}

const staffErrorCodes = {
  ROLE_UNAVAILABLE: "Vai trò đã chọn không còn khả dụng. Chọn vai trò khác.",
  COMPANY_EMAIL_UNAVAILABLE:
    "Không thể tạo email công ty lúc này. Thử lại sau.",
  CONTRACT_REQUIRED:
    "Chưa có hợp đồng. Kiểm tra ngày vào làm và ca làm việc.",
  INVALID_CONTRACT:
    "Hợp đồng hoặc lịch tuần chưa hợp lệ. Kiểm tra loại hợp đồng, ngày và ca làm việc.",
  SHIFT_NOT_FOUND: "Ca làm việc đã chọn không còn tồn tại. Chọn ca khác.",
  SHIFT_UNAVAILABLE: "Ca làm việc đã chọn không còn hoạt động. Chọn ca khác.",
  INVALID_SHIFT_DURATION:
    "Ca làm việc có giờ bắt đầu bằng giờ kết thúc. Kiểm tra ca làm việc.",
  PATTERN_OVERLAP: "Ca làm việc bị trùng trong cùng ngày. Chọn ca khác.",
  EMPLOYEE_NOT_FOUND: "Không tìm thấy hồ sơ nhân viên của tài khoản này.",
  EMPLOYEE_REQUIRED:
    "Chỉ có thể đặt lại mật khẩu cho tài khoản nhân viên.",
  PHONE_ALREADY_EXISTS: "Số điện thoại đã được sử dụng.",
  IDENTITY_CONFLICT:
    "Thông tin định danh đã được sử dụng. Kiểm tra lại CCCD và số điện thoại.",
};

export function getStaffErrorMessage(error, fallback) {
  if (error?.response?.status === 401 || error?.response?.status === 403)
    return "Bạn không có quyền thực hiện thao tác này.";
  const data = error?.response?.data;
  if (data?.code === "STALE_VERSION")
    return "Hãy tải lại hồ sơ trước khi lưu thay đổi.";
  if (data?.code && staffErrorCodes[data.code]) return staffErrorCodes[data.code];
  const fieldErrors = data?.errors ? Object.values(data.errors).flat() : [];
  if (fieldErrors.length) return fieldErrors.join(" ");
  return data?.message || fallback;
}

// Map backend validation keys (PascalCase, dotted paths) back to form fields.
export function setStaffFormErrors(form, error) {
  const errors = error?.response?.data?.errors;
  if (!errors) return false;
  const fields = Object.entries(errors)
    .filter(([, messages]) => Array.isArray(messages) && messages.length)
    .map(([key, messages]) => ({
      name: key.replace(/^Details\./i, "")
        .split(".")
        .map((part) => part.charAt(0).toLowerCase() + part.slice(1)),
      errors: messages,
    }));
  if (!fields.length) return false;
  form.setFields(fields);
  return true;
}

// Same file rules as the admin avatar upload endpoint.
export function validateStaffAvatarFile(file) {
  const allowedTypes = ["image/jpeg", "image/jpg", "image/png"];
  if (!allowedTypes.includes(file?.type))
    return "Bạn chỉ có thể tải lên ảnh JPG hoặc PNG!";
  if (file.size / 1024 / 1024 >= 5) return "Ảnh phải có dung lượng dưới 5MB!";
  return null;
}
