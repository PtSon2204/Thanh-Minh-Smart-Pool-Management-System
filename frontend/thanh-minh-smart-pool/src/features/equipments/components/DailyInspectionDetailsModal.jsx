import { useState, useMemo } from 'react'
import {
  Modal,
  Descriptions,
  Tag,
  Table,
  Typography,
  Button,
  Radio,
  Input,
  Space,
  Badge,
  Segmented,
  Empty,
  notification,
} from 'antd'
import dayjs from 'dayjs'
import {
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  ClockCircleOutlined,
  ToolOutlined,
  SearchOutlined,
} from '@ant-design/icons'
import { useResolveInspectionItem } from '../hooks/useInspections'
import { useAuthStore } from '../../../features/auth/store/authStore'

const { Text } = Typography
const { TextArea } = Input

export default function DailyInspectionDetailsModal({
  open,
  inspection,
  onClose,
  onInspectionUpdated,
}) {
  const isAdmin = useAuthStore((state) => state.session?.role?.toUpperCase() === 'ADMIN')
  const resolveMutation = useResolveInspectionItem()

  // State lọc và tìm kiếm danh sách thiết bị
  const [userSelectedFilter, setUserSelectedFilter] = useState(null)
  const [searchText, setSearchText] = useState('')
  const [prevInspectionId, setPrevInspectionId] = useState(inspection?.id)

  // Tự động reset lựa chọn khi chuyển sang xem biên bản khác
  if (inspection?.id !== prevInspectionId) {
    setPrevInspectionId(inspection?.id)
    setUserSelectedFilter(null)
    setSearchText('')
  }

  // State cho dialog xác nhận xử lý từng thiết bị
  const [resolveTarget, setResolveTarget] = useState(null)
  const [resolveAction, setResolveAction] = useState('Repaired')
  const [resolveNote, setResolveNote] = useState('')

  const allItems = useMemo(() => inspection?.items || [], [inspection])

  const issueItems = useMemo(
    () => allItems.filter((item) => !item.isPassed),
    [allItems]
  )
  const passedItems = useMemo(
    () => allItems.filter((item) => item.isPassed),
    [allItems]
  )

  // Mặc định: nếu có thiết bị sự cố thì ưu tiên chỉ hiển thị thiết bị hỏng trước ('issues'), nếu không thì 'all'
  const filterStatus = userSelectedFilter ?? (issueItems.length > 0 ? 'issues' : 'all')

  const isPassed = inspection?.overallStatus === 'Passed'
  const isResolved = inspection?.overallStatus === 'Resolved'
  const isInResolution = inspection?.overallStatus === 'InResolution'

  const openResolveDialog = (item) => {
    setResolveTarget(item)
    setResolveAction(item.resolutionStatus === 'Replaced' ? 'Replaced' : 'Repaired')
    setResolveNote(item.resolutionNote || '')
  }

  const handleConfirmResolve = async () => {
    if (!resolveTarget) return

    try {
      const updatedInspection = await resolveMutation.mutateAsync({
        inspectionId: inspection.id,
        equipmentId: resolveTarget.equipmentId,
        action: resolveAction,
        note: resolveNote.trim() || null,
      })

      notification.success({
        message: 'Cập nhật thành công',
        description: `Đã xác nhận ${resolveAction === 'Replaced' ? 'thay thế' : 'sửa chữa'} thiết bị "${resolveTarget.equipmentName}". Thiết bị đã được hoàn trả về trạng thái hoạt động (Active).`,
        placement: 'topRight',
      })

      setResolveTarget(null)
      if (onInspectionUpdated) {
        onInspectionUpdated(updatedInspection)
      }
    } catch (error) {
      const serverMsg =
        error?.response?.data?.message ||
        (error?.response?.data?.errors
          ? Object.values(error.response.data.errors).flat().join(' ')
          : error?.response?.data?.title ||
            (error?.response?.status === 404
              ? 'Máy chủ trả về 404 (Chưa tìm thấy API). Hãy dừng và khởi động lại (restart) ứng dụng Backend trong Visual Studio để nhận endpoint mới.'
              : 'Có lỗi xảy ra khi cập nhật trạng thái thiết bị.'))

      notification.error({
        message: 'Cập nhật thất bại',
        description: serverMsg,
        placement: 'topRight',
      })
    }
  }

  // Lọc và sắp xếp thiết bị: sự cố luôn được ưu tiên lên đầu
  const displayedItems = useMemo(() => {
    let list = allItems

    if (filterStatus === 'issues') {
      list = issueItems
    } else if (filterStatus === 'passed') {
      list = passedItems
    }

    if (searchText.trim()) {
      const q = searchText.trim().toLowerCase()
      list = list.filter(
        (item) =>
          (item.equipmentName && item.equipmentName.toLowerCase().includes(q)) ||
          (item.category && item.category.toLowerCase().includes(q)) ||
          (item.location && item.location.toLowerCase().includes(q)) ||
          (item.note && item.note.toLowerCase().includes(q))
      )
    }

    // Sắp xếp ưu tiên:
    // 1. !isPassed lên trước
    // 2. Chờ xử lý (Pending) lên trước Đã xử lý (Repaired/Replaced)
    // 3. Tên thiết bị A-Z
    return [...list].sort((a, b) => {
      if (!a.isPassed && b.isPassed) return -1
      if (a.isPassed && !b.isPassed) return 1

      const aHandled = a.resolutionStatus === 'Repaired' || a.resolutionStatus === 'Replaced'
      const bHandled = b.resolutionStatus === 'Repaired' || b.resolutionStatus === 'Replaced'
      if (!aHandled && bHandled) return -1
      if (aHandled && !bHandled) return 1

      return (a.equipmentName || '').localeCompare(b.equipmentName || '')
    })
  }, [allItems, filterStatus, issueItems, passedItems, searchText])

  const filterOptions = useMemo(
    () => [
      {
        value: 'issues',
        label: (
          <Space size={6} style={{ padding: '0 4px' }}>
            <ExclamationCircleOutlined style={{ color: '#dc2626' }} />
            <span>Sự cố</span>
            <Badge
              count={issueItems.length}
              showZero
              style={{
                backgroundColor: issueItems.length > 0 ? '#ef4444' : '#9ca3af',
                fontSize: 11,
                lineHeight: '16px',
                height: 16,
                minWidth: 16,
              }}
            />
          </Space>
        ),
      },
      {
        value: 'passed',
        label: (
          <Space size={6} style={{ padding: '0 4px' }}>
            <CheckCircleOutlined style={{ color: '#16a34a' }} />
            <span>Đạt chuẩn</span>
            <Badge
              count={passedItems.length}
              showZero
              style={{
                backgroundColor: '#16a34a',
                fontSize: 11,
                lineHeight: '16px',
                height: 16,
                minWidth: 16,
              }}
            />
          </Space>
        ),
      },
      {
        value: 'all',
        label: (
          <Space size={6} style={{ padding: '0 4px' }}>
            <span>Tất cả</span>
            <Badge
              count={allItems.length}
              style={{
                backgroundColor: '#64748b',
                fontSize: 11,
                lineHeight: '16px',
                height: 16,
                minWidth: 16,
              }}
            />
          </Space>
        ),
      },
    ],
    [issueItems.length, passedItems.length, allItems.length]
  )

  const columns = [
    {
      title: 'Thiết bị & Khu vực',
      key: 'equipmentName',
      width: '24%',
      render: (_, item) => (
        <div>
          <strong>{item.equipmentName}</strong>
          {item.location && <div style={{ fontSize: 12, color: '#6b7280' }}>📍 {item.location}</div>}
        </div>
      ),
    },
    {
      title: 'Danh mục',
      dataIndex: 'category',
      key: 'category',
      width: 110,
      render: (cat) => <Tag color="blue">{cat}</Tag>,
    },
    {
      title: 'Đánh giá',
      key: 'isPassed',
      width: 120,
      render: (_, item) =>
        item.isPassed ? (
          <Tag icon={<CheckCircleOutlined />} color="success">
            Đạt chuẩn
          </Tag>
        ) : (
          <Tag icon={<ExclamationCircleOutlined />} color="error">
            Có vấn đề
          </Tag>
        ),
    },
    {
      title: 'Ghi chú sự cố',
      dataIndex: 'note',
      key: 'note',
      width: '24%',
      render: (note, item) =>
        !item.isPassed ? (
          <Text type="danger">{note || 'Không có mô tả chi tiết'}</Text>
        ) : (
          <span style={{ color: '#9ca3af' }}>—</span>
        ),
    },
    {
      title: 'Trạng thái & Xử lý',
      key: 'resolution',
      width: 190,
      render: (_, item) => {
        if (item.isPassed) {
          return <span style={{ color: '#9ca3af' }}>—</span>
        }

        const isRepaired = item.resolutionStatus === 'Repaired'
        const isReplaced = item.resolutionStatus === 'Replaced'
        const isHandled = isRepaired || isReplaced

        return (
          <div>
            {isRepaired && (
              <Tag color="success" icon={<CheckCircleOutlined />}>
                Đã sửa chữa
              </Tag>
            )}
            {isReplaced && (
              <Tag color="cyan" icon={<CheckCircleOutlined />}>
                Đã thay thế mới
              </Tag>
            )}
            {!isHandled && (
              <Tag color="warning" icon={<ClockCircleOutlined />}>
                Chờ xử lý
              </Tag>
            )}

            {isHandled && (
              <div style={{ fontSize: 11, color: '#6b7280', marginTop: 3 }}>
                {item.resolvedBy && <span>Bởi: <strong>{item.resolvedBy}</strong></span>}
                {item.resolvedAt && <div>{dayjs(item.resolvedAt).format('HH:mm - DD/MM')}</div>}
              </div>
            )}

            {item.resolutionNote && (
              <div style={{ fontSize: 11, color: '#374151', fontStyle: 'italic', marginTop: 2 }}>
                "{item.resolutionNote}"
              </div>
            )}

            {isAdmin && (
              <div style={{ marginTop: 6 }}>
                <Button
                  type={isHandled ? 'link' : 'primary'}
                  size="small"
                  icon={<ToolOutlined />}
                  style={!isHandled ? { background: '#0284c7', borderColor: '#0284c7', fontSize: 12 } : { padding: 0, height: 'auto', fontSize: 12 }}
                  onClick={() => openResolveDialog(item)}
                >
                  {isHandled ? 'Cập nhật lại' : 'Xác nhận xử lý'}
                </Button>
              </div>
            )}
          </div>
        )
      },
    },
  ]

  const getOverallTag = () => {
    if (isPassed) {
      return <Tag color="success">Đạt chuẩn vận hành</Tag>
    }
    if (isResolved) {
      return <Tag color="green">Đã khắc phục hoàn tất (Tất cả sự cố đã xử lý)</Tag>
    }
    if (isInResolution) {
      return <Tag color="processing">Đang khắc phục sự cố</Tag>
    }
    return <Tag color="error">Phát hiện sự cố (Cần xử lý)</Tag>
  }

  if (!inspection) return null

  return (
    <>
      <Modal
        title="Chi tiết biên bản kiểm tra thiết bị đầu ca"
        open={open}
        onCancel={onClose}
        footer={null}
        width={880}
        destroyOnClose
      >
        <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }} style={{ marginBottom: 16 }}>
          <Descriptions.Item label="Ca trực">
            <Tag color={inspection.shiftName?.includes('sáng') ? 'orange' : 'purple'}>
              <strong>{inspection.shiftName || 'Ca trực'}</strong>
            </Tag>
          </Descriptions.Item>
          <Descriptions.Item label="Người kiểm tra">
            <strong>{inspection.inspectorName || 'Nhân viên'}</strong>
          </Descriptions.Item>
          <Descriptions.Item label="Thời gian">
            {dayjs(inspection.inspectionTime).format('HH:mm - DD/MM/YYYY')}
          </Descriptions.Item>
          <Descriptions.Item label="Kết quả chung">
            {getOverallTag()}
          </Descriptions.Item>
          <Descriptions.Item label="Thống kê" span={2}>
            <span>{inspection.totalItems} thiết bị ({inspection.issuesCount} hỏng hóc)</span>
          </Descriptions.Item>
          {inspection.notes && (
            <Descriptions.Item label="Ghi chú ca trực" span={2}>
              {inspection.notes}
            </Descriptions.Item>
          )}
        </Descriptions>

        {/* Thanh công cụ lọc thiết bị thông minh */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: 10,
            marginBottom: 12,
            background: '#f8fafc',
            padding: '10px 14px',
            borderRadius: 8,
            border: '1px solid #e2e8f0',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Typography.Title level={5} style={{ margin: 0, fontSize: 15 }}>
              Danh sách thiết bị kiểm tra
            </Typography.Title>
            <Tag color={filterStatus === 'issues' ? 'error' : filterStatus === 'passed' ? 'success' : 'default'}>
              Hiển thị {displayedItems.length} / {allItems.length}
            </Tag>
          </div>

          <Space wrap size="small">
            <Input
              placeholder="Tìm thiết bị..."
              prefix={<SearchOutlined style={{ color: '#9ca3af' }} />}
              allowClear
              size="middle"
              style={{ width: 180 }}
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
            />
            <Segmented
              value={filterStatus}
              onChange={setUserSelectedFilter}
              options={filterOptions}
            />
          </Space>
        </div>

        <Table
          dataSource={displayedItems}
          columns={columns}
          rowKey={(item, idx) => item.equipmentId || idx}
          pagination={
            displayedItems.length > 8
              ? {
                  pageSize: 8,
                  showSizeChanger: true,
                  pageSizeOptions: ['8', '16', '32'],
                  size: 'small',
                  showTotal: (total, range) => `${range[0]}-${range[1]} / ${total} thiết bị`,
                }
              : false
          }
          size="middle"
          locale={{
            emptyText: (
              <Empty
                description={
                  filterStatus === 'issues'
                    ? 'Không có thiết bị nào gặp sự cố'
                    : filterStatus === 'passed'
                    ? 'Không có thiết bị đạt chuẩn'
                    : 'Không tìm thấy thiết bị phù hợp'
                }
                style={{ padding: '24px 0' }}
              />
            ),
          }}
        />
      </Modal>

      {/* Modal xử lý sự cố thiết bị dành cho Admin */}
      <Modal
        title={`Xác nhận xử lý sự cố: ${resolveTarget?.equipmentName || 'Thiết bị'}`}
        open={Boolean(resolveTarget)}
        onCancel={() => setResolveTarget(null)}
        onOk={handleConfirmResolve}
        confirmLoading={resolveMutation.isPending}
        okText="Xác nhận hoàn thành"
        cancelText="Hủy"
        destroyOnClose
        width={480}
      >
        <div style={{ marginTop: 8 }}>
          <div style={{ marginBottom: 8, fontSize: 13, color: '#374151' }}>
            <strong>Sự cố ghi nhận:</strong>{' '}
            <span style={{ color: '#dc2626' }}>{resolveTarget?.note || 'Không có mô tả chi tiết'}</span>
          </div>

          <div style={{ marginBottom: 16 }}>
            <label style={{ display: 'block', marginBottom: 6, fontWeight: 600 }}>
              Hình thức xử lý <span style={{ color: '#dc2626' }}>*</span>:
            </label>
            <Radio.Group
              value={resolveAction}
              onChange={(e) => setResolveAction(e.target.value)}
              buttonStyle="solid"
            >
              <Radio.Button value="Repaired">Đã sửa chữa</Radio.Button>
              <Radio.Button value="Replaced">Đã thay thế mới</Radio.Button>
            </Radio.Group>
          </div>

          <div>
            <label style={{ display: 'block', marginBottom: 6, fontWeight: 600 }}>
              Ghi chú phương án xử lý:
            </label>
            <TextArea
              rows={3}
              placeholder="VD: Đã thay gioăng vòi mới, bơm áp lực đã kiểm tra hoạt động ổn định..."
              value={resolveNote}
              onChange={(e) => setResolveNote(e.target.value)}
              maxLength={500}
              showCount
            />
          </div>

          <div style={{ marginTop: 12, fontSize: 12, color: '#6b7280' }}>
            ℹ️ Sau khi xác nhận, thiết bị sẽ tự động được chuyển về trạng thái <strong>Hoạt động (Active)</strong> để tiếp tục sử dụng.
          </div>
        </div>
      </Modal>
    </>
  )
}
