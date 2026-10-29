import { useState } from 'react'
import { ConfigProvider, Tabs } from 'antd'
import {
  CheckSquareOutlined,
  HistoryOutlined,
  SafetyCertificateOutlined,
} from '@ant-design/icons'
import DailyInspectionForm from '../../../features/equipments/components/DailyInspectionForm'
import DailyInspectionHistory from '../../../features/equipments/components/DailyInspectionHistory'
import '../../../features/equipments/styles/equipmentManagement.css'

export default function DailyInspectionPage() {
  const [activeTab, setActiveTab] = useState('form')

  const tabItems = [
    {
      key: 'form',
      label: (
        <span>
          <CheckSquareOutlined /> Phiếu kiểm tra đầu ca
        </span>
      ),
      children: <DailyInspectionForm onSuccess={() => setActiveTab('history')} />,
    },
    {
      key: 'history',
      label: (
        <span>
          <HistoryOutlined /> Lịch sử kiểm tra
        </span>
      ),
      children: <DailyInspectionHistory />,
    },
  ]

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
                Kiểm tra thiết bị đầu ca (Daily Safety Checklist)
              </h1>
              <p style={{ margin: '4px 0 0 0', color: '#6b7280', fontSize: 14 }}>
                Quy trình kiểm tra vận hành & an toàn trước ca làm việc. Thiết bị phát hiện sự cố sẽ được ghi nhận để thông báo xử lý.
              </p>
            </div>
          </div>
        </header>

        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={tabItems}
          type="card"
          size="large"
        />
      </div>
    </ConfigProvider>
  )
}
