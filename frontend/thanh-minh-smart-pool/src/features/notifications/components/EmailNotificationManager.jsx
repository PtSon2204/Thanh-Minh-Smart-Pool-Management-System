import { useState, useEffect, useCallback } from 'react';
import {
  Card, Form, Input, Select, Button, Table, Tag, Typography,
  Space, Row, Col, Modal, message as antMessage, Divider, Tooltip, Badge
} from 'antd';
import {
  MailOutlined, SendOutlined, EyeOutlined, ReloadOutlined,
  CheckCircleOutlined, InfoCircleOutlined, QrcodeOutlined,
  CalendarOutlined, NotificationOutlined
} from '@ant-design/icons';
import { notificationService } from '../services/notificationService';

const { Title, Text, Paragraph } = Typography;
const { Option } = Select;
const { TextArea } = Input;

const TEMPLATE_PRESETS = {
  ticket_confirmation: {
    label: 'Xác nhận đặt vé bơi ngày',
    icon: <QrcodeOutlined />,
    color: 'blue',
    subject: '[Thanh Minh Pool] Xác nhận đặt vé bơi ngày thành công',
    meta: {
      ticketCode: 'TM-202610-099',
      ticketTypeName: 'Vé bơi tiêu chuẩn 1 lượt (Khu người lớn)',
      amount: '50,000 VND',
      usageDate: new Date().toLocaleDateString('vi-VN'),
      quantity: '1',
    },
    content: '',
  },
  ticket_reminder: {
    label: 'Nhắc hạn gia hạn vé tháng',
    icon: <CalendarOutlined />,
    color: 'orange',
    subject: '[Thanh Minh Pool] Thông báo vé bơi tháng của bạn sắp hết hạn',
    meta: {
      ticketCode: 'VBT-2026-888',
      daysRemaining: '3',
      expiryDate: new Date(Date.now() + 3 * 86400000).toLocaleDateString('vi-VN'),
    },
    content: '',
  },
  announcement: {
    label: 'Thông báo sự cố / bảo trì bể bơi',
    icon: <NotificationOutlined />,
    color: 'red',
    subject: '[Thanh Minh Pool] Thông báo tạm ngưng hoạt động khu bể thiếu nhi để bảo trì lọc nước',
    meta: {
      announcementType: 'Bảo trì định kỳ hệ thống lọc nước Ozone',
    },
    content: 'Ban quản lý xin thông báo khu vực bể thiếu nhi sẽ tạm dừng hoạt động từ 13h00 đến 17h00 ngày hôm nay để sục rửa hệ thống lọc nước tuần hoàn. Khu vực bể bơi tiêu chuẩn 50m vẫn mở cửa đón khách bình thường. Xin cảm ơn quý khách!',
  },
  custom: {
    label: 'Nội dung tùy chỉnh',
    icon: <MailOutlined />,
    color: 'purple',
    subject: '[Thanh Minh Pool] Thư thông báo từ Ban quản lý bể bơi',
    meta: {},
    content: 'Kính gửi quý khách hàng,\n\nCảm ơn bạn đã luôn đồng hành cùng Bể bơi Thông minh Thanh Minh trong thời gian qua.',
  },
};

export default function EmailNotificationManager() {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [history, setHistory] = useState([]);
  const [previewVisible, setPreviewVisible] = useState(false);
  const [previewHtml, setPreviewHtml] = useState('');
  const [selectedTemplate, setSelectedTemplate] = useState('ticket_confirmation');

  // Load initial preset into form
  const applyPreset = useCallback((key) => {
    setSelectedTemplate(key);
    const preset = TEMPLATE_PRESETS[key];
    if (preset) {
      form.setFieldsValue({
        templateType: key,
        subject: preset.subject,
        content: preset.content,
        ticketCode: preset.meta.ticketCode || '',
        ticketTypeName: preset.meta.ticketTypeName || '',
        amount: preset.meta.amount || '',
        usageDate: preset.meta.usageDate || '',
        expiryDate: preset.meta.expiryDate || '',
        daysRemaining: preset.meta.daysRemaining || '',
        announcementType: preset.meta.announcementType || '',
      });
    }
  }, [form]);

  useEffect(() => {
    applyPreset('ticket_confirmation');
    fetchHistory();
  }, [applyPreset]);

  const fetchHistory = async () => {
    setHistoryLoading(true);
    try {
      const res = await notificationService.getEmailHistory(50);
      setHistory(Array.isArray(res) ? res : res?.value || []);
    } catch {
      // Ignored if history service fails
    } finally {
      setHistoryLoading(false);
    }
  };

  const handleTemplateChange = (val) => {
    applyPreset(val);
  };

  const buildPayloadFromValues = (values) => {
    const metadata = {};
    if (values.ticketCode) metadata.ticketCode = values.ticketCode;
    if (values.ticketTypeName) metadata.ticketTypeName = values.ticketTypeName;
    if (values.amount) metadata.amount = values.amount;
    if (values.usageDate) metadata.usageDate = values.usageDate;
    if (values.expiryDate) metadata.expiryDate = values.expiryDate;
    if (values.daysRemaining) metadata.daysRemaining = values.daysRemaining;
    if (values.announcementType) metadata.announcementType = values.announcementType;

    return {
      toEmail: values.toEmail || 'khachhang.demo@gmail.com',
      recipientName: values.recipientName || 'Quý khách',
      subject: values.subject,
      templateType: values.templateType,
      content: values.content,
      metadata,
    };
  };

  const handlePreview = async () => {
    try {
      const values = await form.validateFields();
      const payload = buildPayloadFromValues(values);
      const html = await notificationService.previewEmail(payload);
      setPreviewHtml(html);
      setPreviewVisible(true);
    } catch {
      antMessage.warning('Vui lòng kiểm tra các trường thông tin trước khi xem trước.');
    }
  };

  const handleSend = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);
      const payload = buildPayloadFromValues(values);
      const res = await notificationService.sendEmailNotification(payload);
      if (res && res.success) {
        antMessage.success(res.message || 'Đã gửi email thành công!');
        fetchHistory();
      } else {
        antMessage.error(res?.message || 'Gửi email thất bại.');
      }
    } catch {
      antMessage.error('Đã xảy ra lỗi khi gửi email.');
    } finally {
      setLoading(false);
    }
  };

  const handleViewHistoricalEmail = (htmlBody) => {
    setPreviewHtml(htmlBody);
    setPreviewVisible(true);
  };

  const columns = [
    {
      title: 'Người nhận',
      dataIndex: 'recipient',
      render: (email, record) => (
        <div>
          <Text strong>{record.recipientName || 'Khách hàng'}</Text>
          <br />
          <Text type="secondary" style={{ fontSize: 12 }}>{email}</Text>
        </div>
      ),
    },
    {
      title: 'Tiêu đề email',
      dataIndex: 'subject',
      ellipsis: true,
      render: (subject) => <Text>{subject}</Text>,
    },
    {
      title: 'Loại mẫu',
      dataIndex: 'templateType',
      width: 170,
      render: (type) => {
        const p = TEMPLATE_PRESETS[type] || TEMPLATE_PRESETS.custom;
        return <Tag color={p.color} icon={p.icon}>{p.label}</Tag>;
      },
    },
    {
      title: 'Thời gian',
      dataIndex: 'sentAt',
      width: 160,
      render: (date) => (date ? new Date(date).toLocaleString('vi-VN') : '—'),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 160,
      render: (status) => {
        const isSuccess = status && status.includes('Thành công');
        return (
          <Badge
            status={isSuccess ? 'success' : 'error'}
            text={<Text type={isSuccess ? 'success' : 'danger'}>{status}</Text>}
          />
        );
      },
    },
    {
      title: 'Thao tác',
      width: 100,
      render: (_, record) => (
        <Tooltip title="Xem nội dung thư đã gửi">
          <Button
            size="small"
            icon={<EyeOutlined />}
            onClick={() => handleViewHistoricalEmail(record.htmlBody)}
          >
            Xem
          </Button>
        </Tooltip>
      ),
    },
  ];

  return (
    <div style={{ marginTop: 8 }}>
      <Row gutter={[24, 24]}>
        {/* Cột trái: Form Soạn và Gửi Email */}
        <Col xs={24} lg={11}>
          <Card
            title={
              <Space>
                <MailOutlined style={{ color: '#005f8e' }} />
                <span>Soạn và Gửi Email Thông Báo Tự Động</span>
              </Space>
            }
            bordered={false}
            style={{ borderRadius: 10, boxShadow: '0 2px 8px rgba(0,0,0,0.06)' }}
          >
            <Form
              form={form}
              layout="vertical"
              initialValues={{
                toEmail: 'khachhang.demo@gmail.com',
                recipientName: 'Trần Văn An',
                templateType: 'ticket_confirmation',
              }}
            >
              <Form.Item
                label="Kịch bản Email tự động"
                name="templateType"
                rules={[{ required: true }]}
              >
                <Select onChange={handleTemplateChange}>
                  {Object.entries(TEMPLATE_PRESETS).map(([key, item]) => (
                    <Option key={key} value={key}>
                      <Space>{item.icon} {item.label}</Space>
                    </Option>
                  ))}
                </Select>
              </Form.Item>

              <Row gutter={12}>
                <Col span={14}>
                  <Form.Item
                    label="Email người nhận"
                    name="toEmail"
                    rules={[{ required: true, message: 'Nhập email nhận' }]}
                  >
                    <Input placeholder="ví dụ: khachhang@gmail.com" />
                  </Form.Item>
                </Col>
                <Col span={10}>
                  <Form.Item label="Họ tên người nhận" name="recipientName">
                    <Input placeholder="ví dụ: Nguyễn Văn A" />
                  </Form.Item>
                </Col>
              </Row>

              <Form.Item
                label="Tiêu đề thư"
                name="subject"
                rules={[{ required: true, message: 'Nhập tiêu đề thư' }]}
              >
                <Input />
              </Form.Item>

              {/* Các trường theo từng kịch bản */}
              {selectedTemplate === 'ticket_confirmation' && (
                <div style={{ background: '#f0f9ff', padding: 12, borderRadius: 8, marginBottom: 16 }}>
                  <Text strong style={{ color: '#0284c7', display: 'block', marginBottom: 8 }}>
                    Thông tin vé bơi kèm theo:
                  </Text>
                  <Row gutter={8}>
                    <Col span={12}>
                      <Form.Item label="Mã vé QR" name="ticketCode" style={{ marginBottom: 8 }}>
                        <Input />
                      </Form.Item>
                    </Col>
                    <Col span={12}>
                      <Form.Item label="Số tiền" name="amount" style={{ marginBottom: 8 }}>
                        <Input />
                      </Form.Item>
                    </Col>
                    <Col span={24}>
                      <Form.Item label="Tên loại vé" name="ticketTypeName" style={{ marginBottom: 0 }}>
                        <Input />
                      </Form.Item>
                    </Col>
                  </Row>
                </div>
              )}

              {selectedTemplate === 'ticket_reminder' && (
                <div style={{ background: '#fffbeb', padding: 12, borderRadius: 8, marginBottom: 16 }}>
                  <Text strong style={{ color: '#d97706', display: 'block', marginBottom: 8 }}>
                    Thông tin thẻ vé tháng:
                  </Text>
                  <Row gutter={8}>
                    <Col span={12}>
                      <Form.Item label="Mã thẻ hội viên" name="ticketCode" style={{ marginBottom: 8 }}>
                        <Input />
                      </Form.Item>
                    </Col>
                    <Col span={12}>
                      <Form.Item label="Số ngày còn lại" name="daysRemaining" style={{ marginBottom: 8 }}>
                        <Input />
                      </Form.Item>
                    </Col>
                    <Col span={24}>
                      <Form.Item label="Ngày hết hạn" name="expiryDate" style={{ marginBottom: 0 }}>
                        <Input />
                      </Form.Item>
                    </Col>
                  </Row>
                </div>
              )}

              {(selectedTemplate === 'announcement' || selectedTemplate === 'custom') && (
                <Form.Item
                  label="Nội dung thông báo chi tiết"
                  name="content"
                  rules={[{ required: true, message: 'Nhập nội dung' }]}
                >
                  <TextArea rows={4} placeholder="Nhập nội dung thông báo gửi tới khách hàng..." />
                </Form.Item>
              )}

              <Divider style={{ margin: '16px 0' }} />

              <Space style={{ width: '100%', justifyContent: 'flex-end' }}>
                <Button icon={<EyeOutlined />} onClick={handlePreview}>
                  Xem trước Email
                </Button>
                <Button
                  type="primary"
                  icon={<SendOutlined />}
                  loading={loading}
                  onClick={handleSend}
                  style={{ background: '#005f8e' }}
                >
                  Gửi Email ngay
                </Button>
              </Space>
            </Form>
          </Card>
        </Col>

        {/* Cột phải: Lịch sử gửi email */}
        <Col xs={24} lg={13}>
          <Card
            title={
              <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                <span>Lịch Sử Gửi Email Tự Động</span>
                <Button
                  size="small"
                  icon={<ReloadOutlined />}
                  onClick={fetchHistory}
                  loading={historyLoading}
                >
                  Làm mới
                </Button>
              </Space>
            }
            bordered={false}
            style={{ borderRadius: 10, boxShadow: '0 2px 8px rgba(0,0,0,0.06)' }}
          >
            <Table
              rowKey="id"
              dataSource={history}
              columns={columns}
              loading={historyLoading}
              pagination={{ pageSize: 6, size: 'small' }}
              size="middle"
            />
          </Card>
        </Col>
      </Row>

      {/* Modal Preview Email HTML */}
      <Modal
        title={
          <Space>
            <MailOutlined style={{ color: '#005f8e' }} />
            <span>Xem trước giao diện Email HTML (Khách hàng nhận)</span>
          </Space>
        }
        open={previewVisible}
        onCancel={() => setPreviewVisible(false)}
        footer={[
          <Button key="close" type="primary" onClick={() => setPreviewVisible(false)}>
            Đóng
          </Button>,
        ]}
        width={700}
        destroyOnClose
      >
        <div
          style={{
            maxHeight: '68vh',
            overflowY: 'auto',
            background: '#e2e8f0',
            padding: 16,
            borderRadius: 8,
          }}
        >
          <div
            dangerouslySetInnerHTML={{ __html: previewHtml }}
            style={{ margin: '0 auto' }}
          />
        </div>
      </Modal>
    </div>
  );
}
