import { Card, Row, Col, Statistic, Table, Tag } from 'antd';
import { ArrowUpOutlined, ArrowDownOutlined, UserOutlined, ShoppingCartOutlined, DollarOutlined } from '@ant-design/icons';

const auditLogData = [
  {
    key: '1',
    time: '2023-10-25 10:30:00',
    user: 'Nguyễn Văn A',
    action: 'Cập nhật giá vé',
    details: 'Đã thay đổi giá vé hồ bơi ngoài trời từ 50k sang 60k',
    status: 'Thành công',
  },
  {
    key: '2',
    time: '2023-10-25 09:15:22',
    user: 'Trần Thị B',
    action: 'Xóa người dùng',
    details: 'Đã xóa tài khoản ID #1024',
    status: 'Cảnh báo',
  },
  {
    key: '3',
    time: '2023-10-24 18:45:10',
    user: 'Lê Văn C',
    action: 'Đăng nhập hệ thống',
    details: 'Đăng nhập thành công từ IP 192.168.1.5',
    status: 'Thành công',
  },
  {
    key: '4',
    time: '2023-10-24 15:20:00',
    user: 'Hệ thống',
    action: 'Sao lưu dữ liệu',
    details: 'Tự động sao lưu cơ sở dữ liệu hàng ngày',
    status: 'Thành công',
  },
  {
    key: '5',
    time: '2023-10-23 08:00:00',
    user: 'Nguyễn Văn A',
    action: 'Lỗi đồng bộ',
    details: 'Không thể kết nối với hệ thống cổng kiểm soát vé',
    status: 'Thất bại',
  }
];

const auditLogColumns = [
  {
    title: 'Thời gian',
    dataIndex: 'time',
    key: 'time',
    width: 180,
  },
  {
    title: 'Người thực hiện',
    dataIndex: 'user',
    key: 'user',
    width: 150,
  },
  {
    title: 'Hành động',
    dataIndex: 'action',
    key: 'action',
    width: 200,
  },
  {
    title: 'Chi tiết',
    dataIndex: 'details',
    key: 'details',
  },
  {
    title: 'Trạng thái',
    key: 'status',
    dataIndex: 'status',
    width: 120,
    render: (status) => {
      let color = 'green';
      if (status === 'Thất bại') color = 'volcano';
      if (status === 'Cảnh báo') color = 'gold';
      return (
        <Tag color={color} key={status}>
          {status.toUpperCase()}
        </Tag>
      );
    },
  },
];

export default function DashboardPage() {
  return (
    <div>
      <div style={{ 
        background: 'linear-gradient(90deg, #005f8e 0%, #00b4d8 100%)', 
        padding: '20px 24px', 
        borderRadius: '12px', 
        color: '#fff',
        marginBottom: '24px',
        boxShadow: '0 4px 12px rgba(0, 119, 182, 0.15)'
      }}>
        <h2 style={{ margin: 0, color: '#fff', fontSize: '20px' }}>Xin chào, Quản trị viên! 👋</h2>
        <p style={{ margin: '4px 0 0 0', opacity: 0.9 }}>Dưới đây là tổng quan tình hình kinh doanh ngày hôm nay.</p>
      </div>
      
      <Row gutter={[24, 24]}>
        <Col xs={24} sm={12} lg={8}>
          <Card bordered={false} style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <Statistic
              title="Tổng Khách Hàng"
              value={3782}
              precision={0}
              valueStyle={{ color: '#3f8600', fontWeight: 600 }}
              prefix={<UserOutlined style={{ marginRight: 8 }} />}
              suffix={<small style={{ fontSize: '14px', marginLeft: 8 }}><ArrowUpOutlined /> 11.01%</small>}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card bordered={false} style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <Statistic
              title="Tổng Đơn Hàng"
              value={5359}
              precision={0}
              valueStyle={{ color: '#1677ff', fontWeight: 600 }}
              prefix={<ShoppingCartOutlined style={{ marginRight: 8 }} />}
              suffix={<small style={{ fontSize: '14px', marginLeft: 8, color: '#cf1322' }}><ArrowDownOutlined /> 9.05%</small>}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card bordered={false} style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <Statistic
              title="Doanh Thu Tháng Này"
              value={32870000}
              precision={0}
              valueStyle={{ color: '#faad14', fontWeight: 600 }}
              prefix={<DollarOutlined style={{ marginRight: 8 }} />}
              suffix={<span style={{ fontSize: 16 }}>VNĐ</span>}
            />
          </Card>
        </Col>
      </Row>

      <Card 
        title={<span style={{ fontSize: '18px' }}>Bảng Thông Báo (Audit Logs)</span>}
        bordered={false} 
        style={{ marginTop: 24, borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
      >
        <Table 
          columns={auditLogColumns} 
          dataSource={auditLogData} 
          pagination={{ pageSize: 5 }}
        />
      </Card>
    </div>
  );
}
