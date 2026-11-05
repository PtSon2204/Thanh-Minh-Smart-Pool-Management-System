import { useState } from 'react'
import { ConfigProvider, Button } from 'antd'
import { HistoryOutlined, SettingOutlined } from '@ant-design/icons'
import DailyInspectionHistory from '../../../features/equipments/components/DailyInspectionHistory'
import PoolOperationalSettingsModal from '../../../features/equipments/components/PoolOperationalSettingsModal'
import '../../../features/equipments/styles/equipmentManagement.css'

export default function DailyInspectionHistoryPage() {
  const [settingsOpen, setSettingsOpen] = useState(false)

  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: '#0068ff',
          colorInfo: '#0068ff',
          borderRadius: 8,
        },
      }}
    >
      <div className="equipment-list-page" style={{ padding: '24px' }}>
        <header className="equipment-page-header" style={{ marginBottom: 20, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
          <div className="equipment-page-heading">
            <span className="equipment-header-icon" aria-hidden="true">
              <HistoryOutlined />
            </span>
            <div>
              <h1 style={{ fontSize: 22, fontWeight: 700, margin: 0, color: '#001f3e' }}>
                Lịch sử kiểm tra thiết bị
              </h1>
              <p style={{ margin: '4px 0 0 0', color: '#6b7280', fontSize: 14 }}>
                Theo dõi, tra cứu và kiểm tra chi tiết các biên bản kiểm tra thiết bị đầu ca của nhân viên cứu hộ.
              </p>
            </div>
          </div>

          <Button
            type="primary"
            icon={<SettingOutlined />}
            style={{ background: '#0284c7', borderColor: '#0284c7', height: 40, fontWeight: 600, borderRadius: 8 }}
            onClick={() => setSettingsOpen(true)}
          >
            Cấu hình vận hành & Sức chứa bể
          </Button>
        </header>

        <DailyInspectionHistory />

        <PoolOperationalSettingsModal
          open={settingsOpen}
          onClose={() => setSettingsOpen(false)}
        />
      </div>
    </ConfigProvider>
  )
}

