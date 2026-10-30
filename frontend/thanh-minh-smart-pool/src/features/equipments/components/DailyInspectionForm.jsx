import { useState, useMemo } from 'react'
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
  Select,
} from 'antd'
import {
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  SendOutlined,
  CheckSquareOutlined,
  ToolOutlined,
  InfoCircleOutlined,
  ClockCircleOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { useInspectionForm, useSubmitInspection } from '../hooks/useInspections'

const { Text } = Typography

// 2 Ca cố định của bể bơi
const FIXED_SHIFTS_FALLBACK = [
  {
    shiftId: '0f5c2b1a-7d3e-4a6b-8c9d-1e2f3a4b5c6d',
    name: 'Ca sáng',
    timeRange: '06:00 - 11:30',
    allowedFromTime: '05:30',
    allowedToTime: '11:30',
  },
  {
    shiftId: '1a6d3c2b-8e4f-4b7c-9d0e-2f3a4b5c6d7e',
    name: 'Ca chiều',
    timeRange: '13:30 - 19:30',
    allowedFromTime: '13:00',
    allowedToTime: '19:30',
  },
]

export default function DailyInspectionForm({ onSuccess }) {
  const { data: formData, isLoading, isError, refetch } = useInspectionForm()
  const submitMutation = useSubmitInspection()

  // Hỗ trợ cả object response lẫn array
  const formItems = useMemo(() => {
    if (Array.isArray(formData)) return formData
    return formData?.equipments || []
  }, [formData])

  const shifts = useMemo(() => {
    if (formData?.shifts && formData.shifts.length > 0) return formData.shifts
    return FIXED_SHIFTS_FALLBACK
  }, [formData])

  // Lưu chỉnh sửa của người dùng theo equipmentId
  const [itemEdits, setItemEdits] = useState({})
  const [selectedShiftId, setSelectedShiftId] = useState(null)
  const [generalNotes, setGeneralNotes] = useState('')
  const [confirmModalOpen, setConfirmModalOpen] = useState(false)

  // Xác định ca trực được chọn (mặc định ưu tiên ca đang mở từ backend hoặc ca đầu tiên)
  const effectiveShiftId =
    selectedShiftId ||
    formData?.currentShiftId ||
    shifts.find((s) => s.isOpenNow && !s.isAlreadySubmittedToday)?.shiftId ||
    shifts[0]?.shiftId

  const selectedShift =
    shifts.find((s) => s.shiftId === effectiveShiftId) || shifts[0]

  // Trạng thái kiểm tra thời gian và quyền nộp của ca đang chọn
  const isSelectedShiftAlreadySubmitted = Boolean(selectedShift?.isAlreadySubmittedToday)
  const isSelectedShiftOpen = selectedShift?.isOpenNow !== undefined ? selectedShift.isOpenNow : true
  const canSubmitSelectedShift = isSelectedShiftOpen && !isSelectedShiftAlreadySubmitted

  // Tính toán checklist
  const checklist = formItems.map((item) => {
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
    if (!effectiveShiftId) {
      notification.warning({
        message: 'Chưa chọn ca trực',
        description: 'Vui lòng chọn Ca sáng hoặc Ca chiều để nộp biên bản.',
        placement: 'topRight',
      })
      return
    }

    if (!canSubmitSelectedShift) {
      notification.error({
        message: 'Không thể nộp biên bản',
        description: isSelectedShiftAlreadySubmitted
          ? `${selectedShift?.name} hôm nay đã có biên bản kiểm tra. Mỗi ca chỉ nộp 1 lần.`
          : `Chưa đến khung giờ kiểm tra cho ${selectedShift?.name}. Chỉ mở trước ca trực 30 phút.`,
        placement: 'topRight',
      })
      return
    }

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
        shiftId: effectiveShiftId,
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
            ? `Đã ghi nhận ${issuesCount} sự cố cho ${selectedShift?.name}. Hệ thống đã gửi báo cáo đến Quản trị viên.`
            : `Tất cả thiết bị trong ${selectedShift?.name} đều hoạt động đạt chuẩn. Hồ bơi sẵn sàng phục vụ an toàn.`,
        placement: 'topRight',
        duration: 5,
      })

      if (onSuccess) onSuccess()
    } catch (error) {
      const serverMessage = error?.response?.data?.message
        || (error?.response?.data?.errors
          ? Object.values(error.response.data.errors).flat().join(' ')
          : 'Có lỗi xảy ra khi gửi dữ liệu. Vui lòng thử lại.')

      notification.error({
        message: 'Nộp kiểm tra thất bại',
        description: serverMessage,
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
          <Space align="center">
            <ToolOutlined style={{ color: '#0077b6', fontSize: 16 }} />
            <strong>{record.equipmentName}</strong>
          </Space>
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
              * Vui lòng nhập mô tả sự cố hư hỏng
            </div>
          )}
        </div>
      ),
    },
  ]

  return (
    <div>
      {/* Banner thông báo trạng thái ca trực tổng quát */}
      {formData?.disableReason && (
        <Alert
          type={formData?.canSubmit ? 'info' : 'warning'}
          message={formData?.canSubmit ? 'Thông báo kiểm tra ca trực' : 'Lưu ý khung giờ kiểm tra'}
          description={formData.disableReason}
          showIcon
          style={{ marginBottom: 16, borderRadius: 10 }}
        />
      )}

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

      {/* Chọn ca trực & Gửi biên bản */}
      <Card title="Chọn ca trực & Gửi biên bản kiểm tra" style={{ borderRadius: 10 }}>
        {/* Hàng chọn Ca trực */}
        <div style={{ marginBottom: 16 }}>
          <div style={{ marginBottom: 8, fontWeight: 600, fontSize: 14 }}>
            <ClockCircleOutlined style={{ marginRight: 6, color: '#0077b6' }} />
            Ca trực kiểm tra:
          </div>
          <Select
            style={{ width: '100%', maxWidth: 450 }}
            size="large"
            value={effectiveShiftId}
            onChange={(val) => setSelectedShiftId(val)}
            options={shifts.map((s) => ({
              value: s.shiftId,
              label: (
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span>
                    <strong>{s.name}</strong> ({s.timeRange})
                  </span>
                  {s.isAlreadySubmittedToday ? (
                    <Tag color="blue" style={{ marginLeft: 8 }}>
                      Đã nộp hôm nay
                    </Tag>
                  ) : s.isOpenNow ? (
                    <Tag color="success" style={{ marginLeft: 8 }}>
                      Đang mở kiểm tra
                    </Tag>
                  ) : (
                    <Tag color="default" style={{ marginLeft: 8 }}>
                      Mở từ {s.allowedFromTime}
                    </Tag>
                  )}
                </div>
              ),
            }))}
          />
        </div>

        {/* Alert chi tiết về ca được chọn */}
        {selectedShift && (
          <div style={{ marginBottom: 16 }}>
            {isSelectedShiftAlreadySubmitted ? (
              <Alert
                type="info"
                showIcon
                message={`${selectedShift.name} hôm nay đã được hoàn thành kiểm tra`}
                description={
                  selectedShift.submittedAt
                    ? `Đã được nộp bởi ${selectedShift.submittedByName || 'Cứu hộ'} lúc ${dayjs(selectedShift.submittedAt).format('HH:mm - DD/MM/YYYY')}. Mỗi ca trực chỉ cần kiểm tra 1 lần.`
                    : 'Ca này đã có biên bản kiểm tra trong ngày. Mỗi ca trực chỉ kiểm tra 1 lần.'
                }
              />
            ) : !isSelectedShiftOpen ? (
              <Alert
                type="warning"
                showIcon
                message={`Chưa đến giờ kiểm tra cho ${selectedShift.name}`}
                description={`Quy định an toàn: Chỉ được kiểm tra trước ca trực 30 phút (từ ${selectedShift.allowedFromTime} đến ${selectedShift.allowedToTime}). Vui lòng quay lại trong khung giờ này.`}
              />
            ) : (
              <Alert
                type="success"
                showIcon
                message={`${selectedShift.name} (${selectedShift.timeRange}) đang mở kiểm tra`}
                description={`Khung giờ mở kiểm tra: ${selectedShift.allowedFromTime} - ${selectedShift.allowedToTime}. Hãy rà soát kỹ các thiết bị an toàn và cơ sở vật chất trước khi mở cửa ca.`}
              />
            )}
          </div>
        )}

        {/* Ô ghi chú thêm */}
        <div style={{ marginBottom: 16 }}>
          <div style={{ marginBottom: 8, fontSize: 13, color: '#4b5563' }}>
            Ghi chú bổ sung về ca trực (tùy chọn):
          </div>
          <Input.TextArea
            rows={2}
            placeholder="Ghi nhận thêm về thời tiết, độ trong của nước, khách đông, hoặc lưu ý đặc biệt đầu ca..."
            value={generalNotes}
            onChange={(e) => setGeneralNotes(e.target.value)}
          />
        </div>

        {/* Nút gửi */}
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
            disabled={!canSubmitSelectedShift}
            onClick={handleSubmit}
            style={{
              backgroundColor: !canSubmitSelectedShift
                ? undefined
                : issuesCount > 0
                ? '#ea580c'
                : '#0077b6',
            }}
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
        okText="Xác nhận & Gửi biên bản"
        cancelText="Kiểm tra lại"
        okButtonProps={{ danger: true }}
      >
        <p>
          Bạn đang đánh dấu <strong>{issuesCount}</strong> thiết bị gặp sự cố/không đạt chuẩn cho{' '}
          <strong>{selectedShift?.name}</strong>:
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
          message="Lưu ý"
          description="Biên bản kiểm tra và danh sách sự cố sẽ được lưu vào hệ thống và gửi thông báo tới Quản trị viên. Bạn có chắc chắn muốn nộp?"
          showIcon
          style={{ marginTop: 12 }}
        />
      </Modal>
    </div>
  )
}
