import { Table, Button, Space, Typography, Card, Badge } from 'antd';
import { EyeOutlined, CreditCardOutlined, MoneyCollectOutlined } from '@ant-design/icons';
import './PaymentHistoryPage.css';

const { Title, Text } = Typography;

export default function PaymentHistoryPage() {
  // Mock data tailored for a swimming pool management system
  const data = [
    {
      key: '1',
      invoiceId: 'INV-20230601',
      date: '22/06/2023 08:08',
      ticketType: 'Vé tháng (Người lớn)',
      price: '2.500.000',
      method: 'Chuyển khoản',
      status: 'Success',
    },
    {
      key: '2',
      invoiceId: 'INV-20230602',
      date: '22/06/2023 09:15',
      ticketType: 'Vé lượt (Trẻ em)',
      price: '50.000',
      method: 'Tiền mặt',
      status: 'Success',
    },
    {
      key: '3',
      invoiceId: 'INV-20230603',
      date: '23/06/2023 14:30',
      ticketType: 'Vé tháng (Trẻ em)',
      price: '1.200.000',
      method: 'Chuyển khoản',
      status: 'Failed',
    },
    {
      key: '4',
      invoiceId: 'INV-20230604',
      date: '24/06/2023 10:20',
      ticketType: 'Thuê kính bơi + phao',
      price: '40.000',
      method: 'Tiền mặt',
      status: 'Success',
    },
    {
      key: '5',
      invoiceId: 'INV-20230605',
      date: '25/06/2023 16:45',
      ticketType: 'Vé lượt (Người lớn)',
      price: '80.000',
      method: 'Chuyển khoản',
      status: 'Success',
    },
    {
      key: '6',
      invoiceId: 'INV-20230606',
      date: '26/06/2023 07:10',
      ticketType: 'Gói học bơi (Cơ bản)',
      price: '1.500.000',
      method: 'Chuyển khoản',
      status: 'Success',
    },
    {
      key: '7',
      invoiceId: 'INV-20230607',
      date: '26/06/2023 11:00',
      ticketType: 'Vé tháng (Người lớn)',
      price: '2.500.000',
      method: 'Tiền mặt',
      status: 'Failed',
    },
    {
      key: '8',
      invoiceId: 'INV-20230608',
      date: '27/06/2023 18:30',
      ticketType: 'Vé lượt (Người lớn)',
      price: '80.000',
      method: 'Tiền mặt',
      status: 'Success',
    },
  ];

  const columns = [
    {
      title: 'Mã hóa đơn & Ngày',
      dataIndex: 'date',
      key: 'date',
      width: '20%',
      render: (_, record) => (
        <Space direction="vertical" size={0}>
          <Text strong style={{ color: '#0369a1' }}>{record.invoiceId}</Text>
          <Text type="secondary" style={{ fontSize: '13px' }}>{record.date}</Text>
        </Space>
      ),
    },
    {
      title: 'Loại vé / Dịch vụ',
      key: 'ticketType',
      width: '25%',
      render: (_, record) => (
        <Text strong style={{ color: '#334155' }}>{record.ticketType}</Text>
      ),
    },
    {
      title: 'Phương thức',
      dataIndex: 'method',
      key: 'method',
      width: '20%',
      render: (method) => {
        const isTransfer = method === 'Chuyển khoản';
        return (
          <Space>
            {isTransfer ? 
              <CreditCardOutlined style={{ color: '#0ea5e9' }} /> : 
              <MoneyCollectOutlined style={{ color: '#10b981' }} />
            }
            <Text>{method}</Text>
          </Space>
        );
      }
    },
    {
      title: 'Số tiền (VNĐ)',
      key: 'amount',
      width: '15%',
      render: (_, record) => (
        <Text strong style={{ color: '#f59e0b', fontSize: '15px' }}>
          {record.price} ₫
        </Text>
      ),
    },
    {
      title: 'Trạng thái',
      key: 'status',
      dataIndex: 'status',
      width: '15%',
      render: (status) => {
        const isSuccess = status === 'Success';
        return (
          <Badge 
            status={isSuccess ? 'success' : 'error'} 
            text={
              <Text style={{ 
                color: isSuccess ? '#059669' : '#dc2626',
                fontWeight: 500 
              }}>
                {isSuccess ? 'Thành công' : 'Thất bại'}
              </Text>
            } 
            className={`status-badge ${isSuccess ? 'status-success' : 'status-failed'}`}
          />
        );
      },
    },
    {
      title: 'Hành động',
      key: 'action',
      width: '5%',
      align: 'center',
      render: () => (
        <Button 
          type="primary" 
          shape="circle"
          ghost
          icon={<EyeOutlined />} 
          title="Xem chi tiết"
          style={{ borderColor: '#0ea5e9', color: '#0ea5e9' }}
        />
      ),
    },
  ];

  return (
    <div className="payment-history-page">
      <div className="payment-history-header">
        <Title level={3} style={{ margin: 0, color: '#0f172a' }}>
          Lịch sử giao dịch & thanh toán
        </Title>
        <Text type="secondary">
          Quản lý toàn bộ giao dịch mua vé và dịch vụ tại bể bơi
        </Text>
      </div>
      
      <Card bordered={false} className="payment-table-card" bodyStyle={{ padding: 0 }}>
        <Table 
          columns={columns} 
          dataSource={data} 
          pagination={{ 
            pageSize: 8, 
            showSizeChanger: false,
            position: ['bottomCenter']
          }} 
          rowClassName="payment-table-row"
        />
      </Card>
    </div>
  );
}
