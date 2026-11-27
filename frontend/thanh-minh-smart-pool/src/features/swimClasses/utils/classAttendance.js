export const attendanceStatus = {
  NotMarked: { label: 'Chưa điểm danh', color: 'default' },
  NotApplicable: { label: 'Chưa tham gia lớp', color: 'default' },
  Verified: { label: 'Có mặt', color: 'success' },
  Absent: { label: 'Vắng mặt', color: 'error' },
  Cancelled: { label: 'Buổi đã hủy', color: 'default' },
  PendingReview: { label: 'Đang chờ xử lý (dữ liệu cũ)', color: 'processing' },
  Rejected: { label: 'Chưa xác nhận (dữ liệu cũ)', color: 'warning' },
  Approved: { label: 'Có mặt (dữ liệu cũ)', color: 'success' },
}
