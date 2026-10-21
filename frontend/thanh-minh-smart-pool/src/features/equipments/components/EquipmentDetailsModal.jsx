import { Button, Descriptions, Modal, Tag } from 'antd'
import { EditOutlined, VideoCameraOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { cameraStates, equipmentStatuses, isCamera } from '../utils/equipmentDisplay'
import EquipmentSpecificationsDetails from './EquipmentSpecificationsDetails'

export default function EquipmentDetailsModal({ equipment, onClose, onEdit, onViewCamera }) {
  const status = equipmentStatuses[equipment?.status]
  const connection = equipment?.cameraConnection && cameraStates[equipment.cameraConnection.state]

  return <Modal title="Chi tiết thiết bị" open={Boolean(equipment)} onCancel={onClose} width={640} className="equipment-modal"
    footer={<>
      <Button onClick={onClose}>Đóng</Button>
      {isCamera(equipment?.category) && <Button icon={<VideoCameraOutlined />} disabled={equipment.status !== 'Active'} onClick={() => onViewCamera(equipment)}>Xem camera</Button>}
      <Button type="primary" icon={<EditOutlined />} onClick={() => onEdit(equipment)}>Cập nhật</Button>
    </>}>
    {equipment && <>
      <Descriptions bordered column={1} size="small" items={[
        { key: 'name', label: 'Tên thiết bị', children: <strong>{equipment.name}</strong> },
        { key: 'category', label: 'Nhóm thiết bị', children: equipment.category },
        { key: 'location', label: 'Khu vực', children: equipment.location || 'Chưa xác định' },
        { key: 'status', label: 'Trạng thái', children: <Tag color={status?.color || 'default'}>{status?.label || equipment.status || 'Chưa xác định'}</Tag> },
        { key: 'maintenance', label: 'Bảo trì sắp tới', children: equipment.nextMaintenanceDate ? dayjs(equipment.nextMaintenanceDate).format('DD/MM/YYYY') : 'Chưa có lịch bảo trì' },
        ...(isCamera(equipment.category) ? [{ key: 'source', label: 'Nguồn camera', children: equipment.cameraSource === 'Browser' ? 'Webcam trên máy đang mở trang' : 'Camera IP — RTSP' }] : []),
        ...(equipment.cameraConnection ? [{ key: 'camera', label: 'Kết nối camera', children: <Tag color={connection?.color || 'default'}>{connection?.label || 'Chưa xác định'}</Tag> }] : []),
      ]} />
      <div className="equipment-detail-specifications"><h3>Thông số thiết bị</h3><EquipmentSpecificationsDetails metadata={equipment.metadata} /></div>
    </>}
  </Modal>
}
