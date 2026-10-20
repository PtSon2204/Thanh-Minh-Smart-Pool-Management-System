import { SearchOutlined } from '@ant-design/icons'
import { Input } from 'antd'

export function ServiceListHeader({ title, action }) {
  return <header className="service-list-header"><h1>{title}</h1><div className="service-list-primary-action">{action}</div></header>
}

export function ServiceNameCell({ name }) {
  return (
    <div className="service-name-cell">
      <div className="service-name-heading"><span className="service-type-prefix service-type-rental">CHO THUÊ</span><strong>{name}</strong></div>
    </div>
  )
}

export function ServiceListToolbar({ children, searchTerm, onSearch, disabled = false, searchLabel = 'Tìm dịch vụ' }) {
  return (
    <section className="service-list-toolbar" aria-label="Bộ lọc danh sách">
      <div className="service-list-filters">{children}</div>
      <label className="service-search-control"><span>{searchLabel}</span><Input allowClear aria-label={searchLabel} placeholder="Nhập tên dịch vụ" prefix={<SearchOutlined />} value={searchTerm} disabled={disabled} onChange={(event) => onSearch(event.target.value)} /></label>
    </section>
  )
}
