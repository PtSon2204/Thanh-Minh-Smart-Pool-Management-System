import {
  Drawer,
  Descriptions,
  Tag,
  Alert,
  Space,
  Typography,
  Divider,
} from 'antd';
import {
  CheckCircleOutlined,
  WarningOutlined,
  CloseCircleOutlined,
  ExperimentOutlined,
  CalendarOutlined,
  UserOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';

const { Text, Title, Paragraph } = Typography;

export default function WaterQualityLogDetailDrawer({ visible, log, onClose }) {
  if (!log) return null;

  const renderStatus = (status) => {
    switch (status) {
      case 'WithinLimits':
        return (
          <Tag color="success" icon={<CheckCircleOutlined />}>
            ĐẠT CHUẨN
          </Tag>
        );
      case 'Warning':
        return (
          <Tag color="warning" icon={<WarningOutlined />}>
            CẢNH BÁO
          </Tag>
        );
      case 'Danger':
        return (
          <Tag color="error" icon={<CloseCircleOutlined />}>
            NGUY HIỂM
          </Tag>
        );
      default:
        return <Tag>{status}</Tag>;
    }
  };

  return (
    <Drawer
      title={
        <Space>
          <ExperimentOutlined style={{ color: '#1890ff' }} />
          <span>Chi tiết phiếu kiểm tra chất lượng nước</span>
        </Space>
      }
      placement="right"
      width={540}
      onClose={onClose}
      open={visible}
    >
      {log.status === 'Danger' && (
        <Alert
          message="CẢNH BÁO NGUY HIỂM"
          description="Chỉ số nước vượt ngưỡng an toàn nghiêm trọng. Cần can thiệp hóa chất hoặc tạm ngưng đón khách."
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
        />
      )}

      {log.status === 'Warning' && (
        <Alert
          message="LƯU Ý CẢNH BÁO"
          description="Chỉ số nước nằm ở vùng cảnh báo. Cần xử lý châm hóa chất để đưa về tiêu chuẩn tối ưu."
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
        />
      )}

      <Descriptions title="Thông tin chung" bordered column={1} size="small">
        <Descriptions.Item label="Thời điểm đo">
          <Space>
            <CalendarOutlined />
            <span>{dayjs(log.inspectionTime).format('DD/MM/YYYY HH:mm:ss')}</span>
          </Space>
        </Descriptions.Item>
        <Descriptions.Item label="Người thực hiện đo">
          <Space>
            <UserOutlined />
            <span>{log.inspectorName || 'Hệ thống'}</span>
          </Space>
        </Descriptions.Item>
        <Descriptions.Item label="Nguồn dữ liệu">
          <Tag color={log.source === 'Manual' ? 'blue' : 'purple'}>
            {log.source === 'Manual' ? 'Thủ công (Nhân viên nhập)' : 'Thiết bị cảm biến tự động'}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label="Trạng thái đánh giá">
          {renderStatus(log.status)}
        </Descriptions.Item>
      </Descriptions>

      <Divider style={{ margin: '16px 0' }} />

      <Descriptions title="Chỉ số hóa lý nước" bordered column={1} size="small">
        <Descriptions.Item label="Độ pH">
          <Text strong style={{ fontSize: 16, color: log.phValue < 7.2 || log.phValue > 7.8 ? '#cf1322' : '#389e0d' }}>
            {Number(log.phValue).toFixed(2)}
          </Text>
          <Text type="secondary" style={{ marginLeft: 8 }}>(Chuẩn: 7.20 – 7.80)</Text>
        </Descriptions.Item>
        <Descriptions.Item label="Clo tự do (Free Chlorine)">
          <Text strong style={{ fontSize: 16, color: log.freeChlorineMgL < 1.0 || log.freeChlorineMgL > 3.0 ? '#cf1322' : '#389e0d' }}>
            {Number(log.freeChlorineMgL).toFixed(2)} mg/L
          </Text>
          <Text type="secondary" style={{ marginLeft: 8 }}>(Chuẩn: 1.00 – 3.00 mg/L)</Text>
        </Descriptions.Item>
        <Descriptions.Item label="Độ trong của nước">
          {log.isWaterClear === true && <Tag color="cyan">Trong vắt</Tag>}
          {log.isWaterClear === false && <Tag color="magenta">Bị đục / Có cặn</Tag>}
          {log.isWaterClear === null && <Tag>Không quan sát</Tag>}
        </Descriptions.Item>
      </Descriptions>

      <Divider style={{ margin: '16px 0' }} />

      <Title level={5}>Khuyến nghị xử lý & Châm hóa chất:</Title>
      <div
        style={{
          padding: 12,
          borderRadius: 6,
          background: '#f5f5f5',
          border: '1px solid #d9d9d9',
        }}
      >
        <Paragraph style={{ marginBottom: 0, fontSize: 14 }}>
          {log.recommendation || 'Không có khuyến nghị.'}
        </Paragraph>
      </div>

      {log.notes && (
        <>
          <Divider style={{ margin: '16px 0' }} />
          <Title level={5}>Ghi chú:</Title>
          <Paragraph type="secondary">{log.notes}</Paragraph>
        </>
      )}

      <Divider style={{ margin: '16px 0' }} />
      <div style={{ display: 'flex', justifyContent: 'space-between' }}>
        <Text type="secondary" style={{ fontSize: 12 }}>
          Phiên bản chuẩn: {log.evaluationVersion || 'v1.0'}
        </Text>
        <Text type="secondary" style={{ fontSize: 12 }}>
          Lưu lúc: {dayjs(log.createdAt).format('DD/MM/YYYY HH:mm')}
        </Text>
      </div>
    </Drawer>
  );
}
