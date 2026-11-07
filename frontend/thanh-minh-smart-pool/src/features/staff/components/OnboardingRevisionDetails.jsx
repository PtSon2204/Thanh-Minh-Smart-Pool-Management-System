import { Alert, Descriptions, Tag, Typography } from 'antd'
import { formatOnboardingDate, stateLabels } from '../onboarding/onboardingUi'

export default function OnboardingRevisionDetails({ revision }) {
  if (!revision) return <Alert type="info" showIcon title="Chưa có phiên bản hồ sơ pháp lý" description="Nhân viên hiện có vẫn giữ nguyên quyền và lịch sử. Không yêu cầu nhập lại hồ sơ trước đây." />
  const profile = revision.profile
  return <section className="onboarding-revision" aria-label={`Phiên bản hồ sơ ${revision.revisionNumber}`}>
    <Typography.Title level={4}>Phiên bản {revision.revisionNumber} <Tag>{stateLabels[revision.state] || revision.state}</Tag></Typography.Title>
    <Typography.Paragraph type="secondary">Hồ sơ pháp lý được duyệt là bản chính thức. Hồ sơ cá nhân thông thường chỉ là thông tin hiển thị.</Typography.Paragraph>
    {revision.correctionReason && <Alert type="warning" showIcon title="Lý do / yêu cầu bổ sung" description={revision.correctionReason} />}
    <Descriptions bordered column={{ xs: 1, sm: 2 }} size="small">
      <Descriptions.Item label="Họ và tên">{profile.fullName || '-'}</Descriptions.Item>
      <Descriptions.Item label="Điện thoại">{profile.phone || '-'}</Descriptions.Item>
      <Descriptions.Item label="Ngày sinh">{formatOnboardingDate(profile.dateOfBirth)}</Descriptions.Item>
      <Descriptions.Item label="Số CCCD">{profile.cccdNumber || '-'}</Descriptions.Item>
      <Descriptions.Item label="Thường trú" span={2}>{profile.permanentAddress || '-'}</Descriptions.Item>
      <Descriptions.Item label="Tạm trú" span={2}>{profile.temporaryAddress || '-'}</Descriptions.Item>
      <Descriptions.Item label="Ngân hàng">{profile.bankName || '-'}</Descriptions.Item>
      <Descriptions.Item label="Số tài khoản">{profile.bankAccountNumber || '-'}</Descriptions.Item>
      <Descriptions.Item label="Loại định danh thuế">{profile.taxIdentifierKind === 'LEGACY_TAX_CODE' ? 'Mã số thuế cũ (kiểm tra thủ công)' : 'Số định danh cá nhân'}</Descriptions.Item>
      <Descriptions.Item label="Định danh thuế">{profile.taxIdentifier || '-'}</Descriptions.Item>
      <Descriptions.Item label="Liên hệ khẩn cấp">{profile.emergencyContactName || 'Không cung cấp'}</Descriptions.Item>
      <Descriptions.Item label="Điện thoại liên hệ">{profile.emergencyContactPhone || '-'}</Descriptions.Item>
      <Descriptions.Item label="Mối quan hệ">{profile.emergencyContactRelationship || '-'}</Descriptions.Item>
      <Descriptions.Item label="Gửi duyệt lúc">{formatOnboardingDate(revision.submittedAtUtc, true)}</Descriptions.Item>
      <Descriptions.Item label="Duyệt / phản hồi lúc">{formatOnboardingDate(revision.reviewedAtUtc, true)}</Descriptions.Item>
      <Descriptions.Item label="Người duyệt">{revision.reviewerName || (revision.reviewedAtUtc ? 'Chưa có tên người duyệt' : 'Chưa duyệt')}</Descriptions.Item>
      <Descriptions.Item label="Ảnh CCCD tại thời điểm duyệt">{revision.documentsProvided ? 'Có cung cấp ảnh; xem trạng thái truy cập riêng' : 'Không cung cấp ảnh CCCD'}</Descriptions.Item>
    </Descriptions>
  </section>
}
