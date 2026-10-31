import { ConfigProvider } from 'antd'
import { HistoryOutlined } from '@ant-design/icons'
import DailyInspectionHistory from '../../../features/equipments/components/DailyInspectionHistory'
import '../../../features/equipments/styles/equipmentManagement.css'

export default function DailyInspectionHistoryPage() {
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
              <HistoryOutlined />
            </span>
            <div>
              <h1 style={{ fontSize: 22, fontWeight: 700, margin: 0, color: '#111827' }}>
                Lịch sử kiểm tra thiết bị
              </h1>
              <p style={{ margin: '4px 0 0 0', color: '#6b7280', fontSize: 14 }}>
                Theo dõi, tra cứu và kiểm tra chi tiết các biên bản kiểm tra thiết bị đầu ca của nhân viên cứu hộ.
              </p>
            </div>
          </div>
        </header>

        <DailyInspectionHistory />
      </div>
    </ConfigProvider>
  )
}
