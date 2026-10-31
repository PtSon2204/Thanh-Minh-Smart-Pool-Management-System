import { useNavigate } from 'react-router-dom'
import { ConfigProvider } from 'antd'
import { SafetyCertificateOutlined } from '@ant-design/icons'
import DailyInspectionForm from '../../../features/equipments/components/DailyInspectionForm'
import { useAuthStore } from '../../../features/auth/store/authStore'
import '../../../features/equipments/styles/equipmentManagement.css'

export default function DailyInspectionPage() {
  const navigate = useNavigate()
  const isAdmin = useAuthStore((state) => state.session?.role?.toUpperCase() === 'ADMIN')

  const handleSuccess = () => {
    if (isAdmin) {
      navigate('/admin/equipments/inspection-history')
    }
  }

  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: '#0077b6',
          colorInfo: '#0077b6',
          borderRadius: 8,
        },
      }}
    >
      <div className="equipment-list-page" style={{ padding: '24px' }}>
        <header className="equipment-page-header" style={{ marginBottom: 20 }}>
          <div className="equipment-page-heading">
            <span className="equipment-header-icon" aria-hidden="true">
              <SafetyCertificateOutlined />
            </span>
            <div>
              <h1 style={{ fontSize: 22, fontWeight: 700, margin: 0, color: '#111827' }}>
                Kiểm tra thiết bị đầu ca 
              </h1>
              <p style={{ margin: '4px 0 0 0', color: '#6b7280', fontSize: 14 }}>
                Quy trình kiểm tra vận hành & an toàn trước ca làm việc. Thiết bị phát hiện sự cố sẽ được ghi nhận để thông báo xử lý.
              </p>
            </div>
          </div>
        </header>

        <DailyInspectionForm onSuccess={handleSuccess} />
      </div>
    </ConfigProvider>
  )
}
