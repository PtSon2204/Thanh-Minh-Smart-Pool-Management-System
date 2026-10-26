import { Button, Input, Select } from 'antd'
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons'

const statusOptions = [
  { value: 'Draft', label: 'Bản nháp' },
  { value: 'Active', label: 'Đang hoạt động' },
  { value: 'Inactive', label: 'Ngừng hoạt động' },
]

export default function SwimClassListFilters({ search, status, showStatus, onSearchChange, onStatusChange, onReset }) {
  return <section className="swim-class-list-panel swim-class-list-filters" aria-label="Tìm kiếm và lọc lớp dạy bơi">
    <div className="swim-class-list-panel-heading">
      <h2>Tìm kiếm lớp học</h2>
      <Button type="text" icon={<ReloadOutlined />} onClick={onReset} disabled={!search && !status}>Đặt lại</Button>
    </div>
    <div className="swim-class-list-filter-fields">
      <label>
        <span>Tên lớp</span>
        <Input allowClear maxLength={150} prefix={<SearchOutlined />} placeholder="Nhập tên lớp dạy bơi" value={search} onChange={event => onSearchChange(event.target.value)} />
      </label>
      {showStatus && <label>
        <span>Trạng thái</span>
        <Select allowClear placeholder="Tất cả trạng thái" value={status || undefined} options={statusOptions} onChange={onStatusChange} />
      </label>}
    </div>
  </section>
}
