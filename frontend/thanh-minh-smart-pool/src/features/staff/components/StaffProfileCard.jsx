import { Avatar, Button, Dropdown, Tag } from 'antd'
import { EllipsisOutlined, EditOutlined, EyeOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { USER_ROLES, USER_ROLE_COLORS } from '../../users/userManagement'

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function StaffStatus({ status }) {
  const isWorking = status === 'Working'
  const label = isWorking ? 'Đang làm việc' : status === 'Inactive' ? 'Không hoạt động' : status || 'Chưa xác định'

  return (
    <span className={`staff-profile-status${isWorking ? ' is-working' : status === 'Inactive' ? ' is-inactive' : ''}`}>
      <span className="staff-profile-status-dot" aria-hidden="true" />
      {label}
    </span>
  )
}

function StaffRoleTag({ roleName }) {
  const role = roleName?.toUpperCase()
  const label = USER_ROLES.find((item) => item.value === role)?.label || roleName || 'Chưa có vai trò'

  return <Tag color={USER_ROLE_COLORS[role]} className="staff-profile-role" style={{ color: 'var(--primary-dark)' }}>{label}</Tag>
}

export default function StaffProfileCard({ staff, onDetails, onEdit }) {
  const displayName = staff.fullName || staff.username || 'Nhân viên chưa có tên'
  const initials = displayName.trim().split(/\s+/u).slice(-2).map((part) => part[0]).join('').toLocaleUpperCase('vi-VN')
  const actions = [
    { key: 'view', icon: <EyeOutlined />, label: 'Xem hồ sơ' },
    { key: 'edit', icon: <EditOutlined />, label: 'Sửa hồ sơ việc làm' },
  ]

  const handleAction = ({ key }) => {
    if (key === 'view') onDetails(staff)
    if (key === 'edit') onEdit(staff)
  }

  return (
    <article className="staff-profile-card" aria-label={`Hồ sơ ${displayName}`}>
      <div className="staff-profile-card-identity">
        <div className="staff-profile-card-topline">
          <Avatar
            className="staff-profile-avatar"
            src={staff.avatarUrl || undefined}
            alt={staff.avatarUrl ? `Ảnh đại diện ${displayName}` : undefined}
          >
            {initials}
          </Avatar>
          <Dropdown menu={{ items: actions, onClick: handleAction }} trigger={['click']} placement="bottomRight">
            <Button
              type="text"
              className="staff-profile-menu-trigger"
              icon={<EllipsisOutlined />}
              aria-label={`Tùy chọn hồ sơ ${displayName}`}
            />
          </Dropdown>
        </div>

        <div className="staff-profile-card-name-block">
          <h2 className="staff-profile-card-name">{displayName}</h2>
          <StaffRoleTag roleName={staff.roleName} />
          <span className="staff-profile-username">{staff.username ? `@${staff.username}` : 'Chưa có tên đăng nhập'}</span>
          <StaffStatus status={staff.status} />
        </div>
      </div>

      <dl className="staff-profile-card-details">
        <div className="staff-profile-detail staff-profile-detail-join-date">
          <dt>Ngày vào làm</dt>
          <dd>{staff.joinDate ? dayjs(staff.joinDate).format('DD/MM/YYYY') : '-'}</dd>
        </div>
        <div className="staff-profile-detail staff-profile-detail-salary">
          <dt>Lương cơ bản</dt>
          <dd>{currencyFormatter.format(staff.baseSalary || 0)}</dd>
        </div>
        <div className="staff-profile-detail staff-profile-detail-contact">
          <dt>Email</dt>
          <dd>{staff.email ? staff.email.split(/([@._+-])/u).map((part, index) => index % 2 === 0 ? part : <span key={`${staff.userId}-email-${index}`}>{part}<wbr /></span>) : '-'}</dd>
        </div>
        <div className="staff-profile-detail staff-profile-detail-contact">
          <dt>Điện thoại</dt>
          <dd>{staff.phone || '-'}</dd>
        </div>
      </dl>
    </article>
  )
}
