import { Alert, Descriptions, Typography } from 'antd'
import { contractDays, employmentTypes, formatHours, shiftLabel, weeklyHours } from '../contracts/contractUi'
import dayjs from 'dayjs'
import { formatOnboardingDate, vietnamToday } from '../onboarding/onboardingUi'
import '../styles/staffContracts.css'

export default function ContractTermsSummary({ contract, shifts = [] }) {
  if (!contract) return <Alert type="warning" showIcon title="Chưa có điều khoản hợp đồng" description="Liên hệ quản trị viên để bổ sung hợp đồng trước khi duyệt và sinh lịch." />
  const missingShifts = contract.weeklyPattern?.some((day) => day.shiftIds.some((id) => !shifts.some((shift) => shift.id === id)))
  return <section className="staff-contract-stack" aria-label="Điều khoản hợp đồng chỉ đọc">
    <Descriptions title="Điều khoản do quản trị viên quyết định" bordered size="small" column={{ xs: 1, sm: 2 }}>
      <Descriptions.Item label="Loại hợp đồng">{employmentTypes.find((item) => item.value === contract.employmentType)?.label || contract.employmentType}</Descriptions.Item>
      <Descriptions.Item label="Giờ dự kiến mỗi tuần">{formatHours(weeklyHours(contract.weeklyPattern, shifts))}</Descriptions.Item>
      <Descriptions.Item label="Ngày bắt đầu">{formatOnboardingDate(contract.startDate)}</Descriptions.Item>
      <Descriptions.Item label="Ngày kết thúc (bao gồm)">{formatOnboardingDate(contract.endDate)}</Descriptions.Item>
    </Descriptions>
    <Descriptions title="Lịch tuần theo hợp đồng (chỉ đọc)" bordered size="small" column={1}>
      {contractDays.map((label, index) => <Descriptions.Item key={label} label={label}>{contract.weeklyPattern?.find((day) => day.dayOfWeek === index + 1)?.shiftIds.length
        ? contract.weeklyPattern.find((day) => day.dayOfWeek === index + 1).shiftIds.map((id) => {
        const shift = shifts.find((item) => item.id === id)
        return <div key={id} className="staff-contract-wrap">{shift ? `${shiftLabel(shift)}${shift.isActive === false ? ' · Ngừng hoạt động' : ''}` : 'Chưa có thông tin ca làm việc'}</div>
      }) : 'Nghỉ'}</Descriptions.Item>)}
    </Descriptions>
    {missingShifts && <Alert type="warning" showIcon title="Thiếu tên hoặc thời gian ca" description="Dữ liệu hợp đồng chưa có đủ thông tin ca. Liên hệ quản trị viên để kiểm tra." />}
    <Alert type="info" showIcon title="Ngày hợp đồng và ngày sinh lịch khác nhau" description={`Hợp đồng bao gồm cả ngày bắt đầu và ngày kết thúc. Nếu duyệt hoặc thay đổi hôm nay, chỉ sinh lịch từ ${dayjs(vietnamToday()).add(1, 'day').format('DD/MM/YYYY')} trở đi (giờ Việt Nam), trong thời hạn hợp đồng. Không sinh bù cho hôm nay hoặc quá khứ.`} />
    <Typography.Paragraph type="secondary">Nhân viên chỉ xem điều khoản này. Chỉnh sửa hồ sơ pháp lý không thay đổi hợp đồng hoặc lịch tuần.</Typography.Paragraph>
  </section>
}
