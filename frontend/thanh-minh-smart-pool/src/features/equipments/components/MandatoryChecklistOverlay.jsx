import { useCallback } from 'react'
import { Modal, Alert, Typography, notification } from 'antd'
import { SafetyCertificateOutlined } from '@ant-design/icons'
import { useQueryClient } from '@tanstack/react-query'
import { useMandatoryChecklistStatus } from '../hooks/useMandatoryChecklistStatus'
import DailyInspectionForm from './DailyInspectionForm'
import '../styles/equipmentManagement.css'

const { Title, Text } = Typography

export default function MandatoryChecklistOverlay() {
  const queryClient = useQueryClient()
  const { data: status } = useMandatoryChecklistStatus()

  const isLocked = Boolean(status?.hasPendingMandatoryChecklist)

  const handleSuccess = useCallback(() => {
    notification.success({
      message: 'Kiểm tra thiết bị hoàn tất!',
      description: `Biên bản kiểm tra ${status?.shiftName || 'đầu ca'} đã được ghi nhận. Hệ thống đã mở khóa đầy đủ chức năng. Chúc bạn có ca làm việc an toàn và hiệu quả!`,
      placement: 'topRight',
      duration: 6,
    })

    queryClient.invalidateQueries({ queryKey: ['mandatory-checklist-status'] })
    queryClient.invalidateQueries({ queryKey: ['equipment-inspection-form'] })
    queryClient.invalidateQueries({ queryKey: ['operational-tasks'] })
  }, [queryClient, status])

  if (!isLocked) {
    return null
  }

  return (
    <Modal
      open={isLocked}
      closable={false}
      maskClosable={false}
      keyboard={false}
      footer={null}
      centered
      width={1160}
      zIndex={2000}
      className="mandatory-checklist-modal"
      styles={{
        mask: {
          backdropFilter: 'blur(5px)',
          backgroundColor: 'rgba(0, 15, 30, 0.85)',
          zIndex: 2000,
        },
        wrapper: {
          zIndex: 2001,
        },
        content: {
          borderRadius: 12,
          padding: '24px 28px',
          boxShadow: '0 20px 40px rgba(0, 0, 0, 0.3)',
        },
        body: {
          maxHeight: '82vh',
          overflowY: 'auto',
          paddingRight: 6,
        },
      }}
    >
      <div style={{ marginBottom: 20 }}>
        <Alert
          type="warning"
          showIcon
          icon={<SafetyCertificateOutlined style={{ fontSize: 24, color: '#d46b08' }} />}
          message={
            <Title level={4} style={{ margin: 0, color: '#d46b08' }}>
              Quy định an toàn: Hoàn thành kiểm tra thiết bị đầu ca làm việc
            </Title>
          }
          description={
            <div style={{ marginTop: 4 }}>
              <Text>
                Bạn đã được phân công nhiệm vụ kiểm tra thiết bị an toàn cho{' '}
                <strong>{status?.shiftName || 'ca trực hôm nay'}</strong> ({status?.timeRange || 'đầu ca'}).
                Theo quy chuẩn an toàn bể bơi, biên bản kiểm tra phải được nộp trước khi mở cửa đón khách.
              </Text>
            </div>
          }
          style={{
            borderRadius: 8,
            border: '1px solid #ffe58f',
            backgroundColor: '#fffbe6',
          }}
        />
      </div>

      <DailyInspectionForm
        key={status?.taskId || 'mandatory'}
        operationalTaskId={status?.taskId}
        initialShiftId={status?.shiftId}
        onSuccess={handleSuccess}
      />
    </Modal>
  )
}
