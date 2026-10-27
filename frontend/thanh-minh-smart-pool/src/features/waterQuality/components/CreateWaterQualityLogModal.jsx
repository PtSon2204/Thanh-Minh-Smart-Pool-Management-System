import { useState, useCallback } from 'react';
import {
  Modal,
  Form,
  InputNumber,
  DatePicker,
  Radio,
  Input,
  Tag,
  Space,
  Typography,
  message,
} from 'antd';
import {
  ExperimentOutlined,
  CheckCircleOutlined,
  WarningOutlined,
  CloseCircleOutlined,
  MedicineBoxOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { waterQualityService } from '../services/waterQualityService';

const { Text, Paragraph } = Typography;

const DEFAULT_PREVIEW = {
  status: 'WithinLimits',
  recommendation: 'Chất lượng nước đạt chuẩn (pH: 7.40, Clo: 2.00 mg/L). Duy trì lọc tuần hoàn và đo định kỳ.',
  findings: [],
  suggestedChemicals: [],
};

export default function CreateWaterQualityLogModal({ visible, onCancel, onSuccess }) {
  const [form] = Form.useForm();
  const [submitting, setSubmitting] = useState(false);
  const [preview, setPreview] = useState(DEFAULT_PREVIEW);

  // Live evaluation based on values
  const handleValuesChange = useCallback((changedValues, allValues) => {
    const ph = allValues.phValue;
    const cl = allValues.freeChlorineMgL;
    const isClear = allValues.isWaterClear;

    if (ph !== undefined && ph !== null && cl !== undefined && cl !== null && ph >= 0 && cl >= 0) {
      waterQualityService
        .previewEvaluation({
          phValue: Number(ph),
          freeChlorineMgL: Number(cl),
          isWaterClear: isClear,
        })
        .then((res) => setPreview(res))
        .catch(() => setPreview(null));
    } else {
      setPreview(null);
    }
  }, []);

  const handleModalAfterClose = () => {
    form.resetFields();
    setPreview(DEFAULT_PREVIEW);
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setSubmitting(true);

      const payload = {
        inspectionTime: values.inspectionTime.toISOString(),
        phValue: Number(values.phValue),
        freeChlorineMgL: Number(values.freeChlorineMgL),
        isWaterClear: values.isWaterClear,
        notes: values.notes?.trim() || null,
      };

      const res = await waterQualityService.createLog(payload);
      message.success('Đã ghi nhận phiếu đo chất lượng nước thành công!');

      if (res.status === 'Danger') {
        message.error('CHÚ Ý: Chỉ số đo ở mức NGUY HIỂM. Hệ thống đã gửi cảnh báo tới Ban quản trị!');
      }

      onSuccess(res);
      onCancel();
    } catch (error) {
      if (error?.response?.data?.message) {
        message.error(error.response.data.message);
      } else if (error?.errorFields) {
        // Antd validation error, ignore
      } else {
        message.error('Có lỗi xảy ra khi lưu phiếu đo.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  const renderStatusBadge = (status) => {
    switch (status) {
      case 'WithinLimits':
        return (
          <Tag color="success" icon={<CheckCircleOutlined />}>
            ĐẠT CHUẨN (AN TOÀN)
          </Tag>
        );
      case 'Warning':
        return (
          <Tag color="warning" icon={<WarningOutlined />}>
            CẢNH BÁO LỆCH CHUẨN
          </Tag>
        );
      case 'Danger':
        return (
          <Tag color="error" icon={<CloseCircleOutlined />}>
            NGUY HIỂM — CẦN XỬ LÝ GẤP
          </Tag>
        );
      default:
        return <Tag>Chưa đánh giá</Tag>;
    }
  };

  return (
    <Modal
      title={
        <Space>
          <ExperimentOutlined style={{ color: '#1890ff', fontSize: 20 }} />
          <span>Tạo phiếu kiểm tra chất lượng nước</span>
        </Space>
      }
      open={visible}
      onOk={handleSubmit}
      onCancel={onCancel}
      afterClose={handleModalAfterClose}
      confirmLoading={submitting}
      okText="Lưu phiếu đo"
      cancelText="Hủy"
      width={680}
      destroyOnClose
    >
      <Form
        form={form}
        layout="vertical"
        onValuesChange={handleValuesChange}
        initialValues={{
          inspectionTime: dayjs(),
          phValue: 7.4,
          freeChlorineMgL: 2.0,
          isWaterClear: true,
        }}
      >
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
          <Form.Item
            name="inspectionTime"
            label="Thời điểm đo"
            rules={[{ required: true, message: 'Vui lòng chọn thời gian đo' }]}
          >
            <DatePicker
              showTime
              format="DD/MM/YYYY HH:mm"
              style={{ width: '100%' }}
              disabledDate={(current) => current && current > dayjs().endOf('day')}
            />
          </Form.Item>

          <Form.Item
            name="isWaterClear"
            label="Độ trong của nước"
            rules={[{ required: true, message: 'Vui lòng chọn tình trạng nước' }]}
          >
            <Radio.Group style={{ width: '100%' }}>
              <Radio.Button value={true} style={{ width: '50%', textAlign: 'center' }}>
                Trong vắt
              </Radio.Button>
              <Radio.Button value={false} style={{ width: '50%', textAlign: 'center' }}>
                Bị đục / Có cặn
              </Radio.Button>
            </Radio.Group>
          </Form.Item>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
          <Form.Item
            name="phValue"
            label={
              <span>
                Độ pH <Text type="secondary">(Chuẩn: 7.20 – 7.80)</Text>
              </span>
            }
            rules={[
              { required: true, message: 'Vui lòng nhập chỉ số pH' },
              { type: 'number', min: 0, max: 14, message: 'pH từ 0 đến 14' },
            ]}
          >
            <InputNumber
              step={0.1}
              precision={2}
              style={{ width: '100%' }}
              placeholder="VD: 7.4"
            />
          </Form.Item>

          <Form.Item
            name="freeChlorineMgL"
            label={
              <span>
                Clo tự do (mg/L) <Text type="secondary">(Chuẩn: 1.00 – 3.00)</Text>
              </span>
            }
            rules={[
              { required: true, message: 'Vui lòng nhập nồng độ Clo tự do' },
              { type: 'number', min: 0, max: 20, message: 'Clo từ 0 đến 20 mg/L' },
            ]}
          >
            <InputNumber
              step={0.1}
              precision={2}
              style={{ width: '100%' }}
              placeholder="VD: 1.8"
            />
          </Form.Item>
        </div>

        {/* Live System Chemical & Health Evaluation Card */}
        {preview && (
          <div
            style={{
              marginTop: 8,
              marginBottom: 16,
              padding: 14,
              borderRadius: 8,
              border: `1px solid ${
                preview.status === 'Danger'
                  ? '#ffccc7'
                  : preview.status === 'Warning'
                  ? '#ffe58f'
                  : '#b7eb8f'
              }`,
              background:
                preview.status === 'Danger'
                  ? '#fff1f0'
                  : preview.status === 'Warning'
                  ? '#fffbe6'
                  : '#f6ffed',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Space>
                <MedicineBoxOutlined style={{ fontSize: 18 }} />
                <Text strong>Đánh giá & Gợi ý châm hóa chất tức thì:</Text>
              </Space>
              {renderStatusBadge(preview.status)}
            </div>

            {preview.findings && preview.findings.length > 0 && (
              <div style={{ marginTop: 8 }}>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  Phát hiện:
                </Text>
                <Space size={[0, 4]} wrap style={{ marginLeft: 8 }}>
                  {preview.findings.map((finding, idx) => (
                    <Tag color="volcano" key={idx}>
                      {finding}
                    </Tag>
                  ))}
                </Space>
              </div>
            )}

            {preview.suggestedChemicals && preview.suggestedChemicals.length > 0 && (
              <div style={{ marginTop: 8 }}>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  Hóa chất đề xuất:
                </Text>
                <Space size={[0, 4]} wrap style={{ marginLeft: 8 }}>
                  {preview.suggestedChemicals.map((chem, idx) => (
                    <Tag color="geekblue" key={idx} style={{ fontWeight: 600 }}>
                      {chem}
                    </Tag>
                  ))}
                </Space>
              </div>
            )}

            <Paragraph
              style={{
                marginTop: 8,
                marginBottom: 0,
                fontSize: 13,
                color: preview.status === 'Danger' ? '#cf1322' : '#262626',
              }}
            >
              {preview.recommendation}
            </Paragraph>
          </div>
        )}

        <Form.Item name="notes" label="Ghi chú thêm">
          <Input.TextArea
            rows={2}
            maxLength={500}
            showCount
            placeholder="Ghi chú về tình hình thời tiết, lượng khách trong hồ, vị trí lấy mẫu..."
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}
