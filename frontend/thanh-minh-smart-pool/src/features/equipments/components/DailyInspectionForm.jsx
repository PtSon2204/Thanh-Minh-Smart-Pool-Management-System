import { useState } from 'react'
import {
  Card,
  Table,
  Radio,
  Input,
  Button,
  Tag,
  Space,
  Alert,
  Spin,
  Empty,
  Modal,
  notification,
  Typography,
} from 'antd'
import {
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  SendOutlined,
  CheckSquareOutlined,
  InfoCircleOutlined,
} from '@ant-design/icons'
import { useInspectionForm, useSubmitInspection } from '../hooks/useInspections'

const { Text } = Typography

export default function DailyInspectionForm({ onSuccess }) {
  const { data: formItems, isLoading, isError, refetch } = useInspectionForm()
  const submitMutation = useSubmitInspection()

  // Lưu các chỉnh sửa của người dùng theo equipmentId: { isPassed: boolean, note: string }
  const [itemEdits, setItemEdits] = useState({})
  const [generalNotes, setGeneralNotes] = useState('')
  const [confirmModalOpen, setConfirmModalOpen] = useState(false)

  // Tính toán danh sách checklist trực tiếp từ formItems và itemEdits mà không cần useEffect
  const checklist = (formItems || []).map((item) => {
    const edit = itemEdits[item.equipmentId]
    return {
      equipmentId: item.equipmentId,
      equipmentName: item.name,
      category: item.category,
      location: item.location,
      isPassed: edit?.isPassed !== undefined ? edit.isPassed : true,
      note: edit?.note !== undefined ? edit.note : '',
    }
  })

  const handleStatusChange = (equipmentId, isPassed) => {
    setItemEdits((prev) => ({
      ...prev,
      [equipmentId]: {
        ...prev[equipmentId],
        isPassed,
        note: isPassed ? '' : (prev[equipmentId]?.note || ''),
      },
    }))
  }

  const handleNoteChange = (equipmentId, note) => {
    setItemEdits((prev) => ({
      ...prev,
      [equipmentId]: {
        ...prev[equipmentId],
        note,
      },
    }))
  }

  const handleMarkAllPassed = () => {
    setItemEdits({})
  }

  const issuesCount = checklist.filter((item) => !item.isPassed).length
  const passedCount = checklist.length - issuesCount

  const handleSubmit = async () => {
    // Kiểm tra nếu có thiết bị hỏng mà chưa nhập mô tả
    const missingNotes = checklist.filter((item) => !item.isPassed && !item.note?.trim())
    if (missingNotes.length > 0) {
      notification.warning({
        message: 'Thiếu thông tin sự cố',
        description: `Vui lòng nhập mô tả tình trạng hư hỏng cho ${missingNotes.length} thiết bị được đánh dấu "Có vấn đề" để đội kỹ thuật có thể tiếp nhận sửa chữa.`,
        placement: 'topRight',
      })
      return
    }

    if (issuesCount > 0) {
      setConfirmModalOpen(true)
    } else {
      executeSubmit()
    }
  }

  const executeSubmit = async () => {
    try {
      const payload = {
        shiftId: null,
        notes: generalNotes.trim() || null,
        items: checklist.map((c) => ({
          equipmentId: c.equipmentId,
          equipmentName: c.equipmentName,
          category: c.category,
          location: c.location,
          isPassed: c.isPassed,
          note: c.note?.trim() || null,
        })),
      }

      await submitMutation.mutateAsync(payload)
      setConfirmModalOpen(false)

      notification.success({
        message: 'Nộp biên bản kiểm tra thành công!',
        description:
          issuesCount > 0
            ? `Đã ghi nhận ${issuesCount} sự cố. Các thiết bị này đã được tự động khóa sang trạng thái Bảo trì và tạo lịch sửa chữa khẩn.`
            : 'Tất cả thiết bị đều hoạt động đạt chuẩn. Hồ bơi sẵn sàng phục vụ an toàn.',
        placement: 'topRight',
        duration: 5,
      })

      if (onSuccess) onSuccess()
    } catch (error) {
      notification.error({
        message: 'Nộp kiểm tra thất bại',
        description: error?.response?.data?.message || 'Có lỗi xảy ra khi gửi dữ liệu. Vui lòng thử lại.',
        placement: 'topRight',
      })
    }
  }

  if (isLoading) {
    return (
      <div style={{ textAlign: 'center', padding: '60px 0' }}>
        <Spin size="large" tip="Đang tải danh sách thiết bị cần kiểm tra..." />
      </div>
    )
  }

  if (isError) {
    return (
      <Alert
        type="error"
        message="Không thể tải danh mục thiết bị"
        description="Đã xảy ra lỗi khi lấy danh sách thiết bị vận hành và an toàn. Vui lòng bấm thử lại."
        action={
          <Button size="small" type="primary" onClick={() => refetch()}>
            Thử lại
          </Button>
        }
        showIcon
      />
    )
  }

  if (!formItems || formItems.length === 0) {
    return (
      <Empty
        description="Hiện không có thiết bị vận hành hoặc an toàn nào cần kiểm tra đầu ca."
        style={{ padding: '40px 0' }}
      />
    )
  }

  const columns = [
    {
      title: 'Thiết bị & Khu vực',
      key: 'equipment',
      width: '32%',
      render: (_, record) => (
        <div>
          <strong>{record.equipmentName}</strong>
          <div style={{ marginTop: 4 }}>
            <Tag color="cyan">{record.category}</Tag>
            {record.location && (
              <span style={{ fontSize: 12, color: '#6b7280' }}>
                📍 {record.location}
              </span>
            )}
          </div>
        </div>
      ),
    },
    {
      title: 'Tình trạng kiểm tra',
      key: 'status',
      width: '28%',
      render: (_, record) => (
        <Radio.Group
          value={record.isPassed}
          onChange={(e) => handleStatusChange(record.equipmentId, e.target.value)}
          buttonStyle="solid"
          size="middle"
        >
          <Radio.Button value={true} style={{ color: record.isPassed ? '#fff' : '#16a34a' }}>
            <CheckCircleOutlined /> Đạt chuẩn
          </Radio.Button>
          <Radio.Button value={false} style={{ color: !record.isPassed ? '#fff' : '#dc2626' }}>
            <ExclamationCircleOutlined /> Có vấn đề
          </Radio.Button>
        </Radio.Group>
      ),
    },
    {
      title: 'Ghi chú hư hỏng / Chi tiết sự cố',
      key: 'note',
      width: '40%',
      render: (_, record) => (
        <div>
          <Input
            placeholder={
              record.isPassed
                ? 'Ghi chú bổ sung (nếu có)...'
                : 'Mô tả hư hỏng (bắt buộc: xịt hơi, nứt, chập điện...)'
            }
            status={!record.isPassed && !record.note?.trim() ? 'error' : ''}
            value={record.note}
            onChange={(e) => handleNoteChange(record.equipmentId, e.target.value)}
            allowClear
          />
          {!record.isPassed && !record.note?.trim() && (
            <div style={{ color: '#dc2626', fontSize: 11, marginTop: 4 }}>
              * Vui lòng nhập mô tả để tạo phiếu bảo trì
            </div>
          )}
        </div>
      ),
    },
  ]

  return (
    <div>
      {/* Thanh tổng quan & Thao tác nhanh */}
      <Card
        style={{ marginBottom: 16, borderRadius: 10 }}
        styles={{ body: { padding: '16px 20px' } }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
          <Space size="middle" wrap>
            <Tag color="blue" style={{ padding: '4px 10px', fontSize: 13 }}>
              Tổng thiết bị: <strong>{checklist.length}</strong>
            </Tag>
            <Tag color="success" style={{ padding: '4px 10px', fontSize: 13 }}>
              Đạt chuẩn: <strong>{passedCount}</strong>
            </Tag>
            {issuesCount > 0 && (
              <Tag color="error" style={{ padding: '4px 10px', fontSize: 13 }}>
                Phát hiện sự cố: <strong>{issuesCount}</strong>
              </Tag>
            )}
          </Space>

          <Button
            type="dashed"
            icon={<CheckSquareOutlined />}
            onClick={handleMarkAllPassed}
            style={{ borderColor: '#16a34a', color: '#16a34a' }}
          >
            Đánh dấu tất cả đạt yêu cầu
          </Button>
        </div>
      </Card>

      {/* Bảng checklist thiết bị */}
      <Card style={{ borderRadius: 10, marginBottom: 16 }}>
        <Table
          dataSource={checklist}
          columns={columns}
          rowKey="equipmentId"
          pagination={false}
          size="middle"
          rowClassName={(record) => (!record.isPassed ? 'table-row-error' : '')}
        />
      </Card>

      {/* Ghi chú chung & Submit */}
      <Card title="Ghi chú ca trực & Gửi biên bản" style={{ borderRadius: 10 }}>
        <Input.TextArea
          rows={3}
          placeholder="Ghi chú tổng quát về tình hình trước khi mở cửa ca trực (ví dụ: thời tiết, độ trong của nước, ghi nhận đặc biệt)..."
          value={generalNotes}
          onChange={(e) => setGeneralNotes(e.target.value)}
          style={{ marginBottom: 16 }}
        />

        <div style={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center', gap: 12 }}>
          {issuesCount > 0 && (
            <Text type="danger" style={{ fontSize: 13 }}>
              <InfoCircleOutlined /> Có {issuesCount} thiết bị sẽ được tự động khóa và tạo lịch sửa chữa khẩn.
            </Text>
          )}

          <Button
            type="primary"
            size="large"
            icon={<SendOutlined />}
            loading={submitMutation.isPending}
            onClick={handleSubmit}
            style={{ backgroundColor: issuesCount > 0 ? '#ea580c' : '#0077b6' }}
          >
            Hoàn thành & Gửi biên bản kiểm tra
          </Button>
        </div>
      </Card>

      {/* Modal xác nhận nếu có thiết bị báo hỏng */}
      <Modal
        title="Xác nhận gửi biên bản có thiết bị hư hỏng"
        open={confirmModalOpen}
        onOk={executeSubmit}
        confirmLoading={submitMutation.isPending}
        onCancel={() => setConfirmModalOpen(false)}
        okText="Xác nhận & Khóa bảo trì"
        cancelText="Kiểm tra lại"
        okButtonProps={{ danger: true }}
      >
        <p>
          Bạn đang đánh dấu <strong>{issuesCount}</strong> thiết bị gặp sự cố/không đạt chuẩn:
        </p>
        <ul style={{ paddingLeft: 20, color: '#dc2626' }}>
          {checklist
            .filter((item) => !item.isPassed)
            .map((item) => (
              <li key={item.equipmentId} style={{ marginBottom: 4 }}>
                <strong>{item.equipmentName}</strong>: {item.note || 'Chưa ghi chú'}
              </li>
            ))}
        </ul>
        <Alert
          type="warning"
          message="Hành động tự động của hệ thống"
          description="Các thiết bị trên sẽ ngay lập tức được chuyển sang trạng thái 'Bảo trì' và tạo công việc sửa chữa khẩn cấp cho đội kỹ thuật. Bạn có chắc chắn muốn nộp?"
          showIcon
          style={{ marginTop: 12 }}
        />
      </Modal>
    </div>
  )
}
