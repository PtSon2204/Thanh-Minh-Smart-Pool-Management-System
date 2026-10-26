import { useEffect, useState } from 'react'
import { Alert, Button, ConfigProvider, Pagination } from 'antd'
import { BookOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import { useAuthStore } from '../../auth/store/authStore'
import { useSwimClasses } from '../hooks/useSwimClasses'
import SwimClassListFilters from './SwimClassListFilters'
import SwimClassListTable from './SwimClassListTable'
import '../styles/swimClassList.css'

export default function SwimClassListView() {
  const session = useAuthStore(state => state.session)
  const navigate = useNavigate()
  const role = session?.role?.toUpperCase()
  const isAdmin = role === 'ADMIN'
  const [search, setSearch] = useState('')
  const [params, setParams] = useState({ searchTerm: '', status: undefined, pageIndex: 1, pageSize: 10 })
  const list = useSwimClasses(params, session?.id)
  const filtered = Boolean(params.searchTerm || params.status)

  useEffect(() => {
    const timer = setTimeout(() => {
      const term = search.trim()
      setParams(previous => previous.searchTerm === term ? previous : { ...previous, searchTerm: term, pageIndex: 1 })
    }, 350)
    return () => clearTimeout(timer)
  }, [search])

  function resetFilters() {
    setSearch('')
    setParams(previous => ({ ...previous, searchTerm: '', status: undefined, pageIndex: 1 }))
  }

  const title = isAdmin ? 'Quản lý lớp dạy bơi' : role === 'COACH' ? 'Lớp được phân công' : 'Lớp dạy bơi'
  const intro = isAdmin ? 'Theo dõi lớp, lịch nhận đăng ký và số học viên đã xác nhận.'
    : role === 'COACH' ? 'Các lớp có buổi học được phân công cho bạn.'
      : 'Khám phá các lớp học bơi đang hoạt động.'

  return <ConfigProvider theme={{ token: { colorPrimary: '#0077b6', colorInfo: '#0077b6', borderRadius: 8 } }}>
    <div className={`swim-class-list-page${role === 'CUSTOMER' ? ' swim-class-list-page--customer' : ''}`}>
      <header className="swim-class-list-header">
        <div className="swim-class-list-heading"><span className="swim-class-list-icon"><BookOutlined /></span><div><h1>{title}</h1><p>{intro}</p></div></div>
        {isAdmin && <Button type="primary" size="large" icon={<PlusOutlined />} onClick={() => navigate('/admin/classes/create')}>Tạo lớp</Button>}
      </header>

      <SwimClassListFilters search={search} status={params.status} showStatus={role !== 'CUSTOMER'}
        onSearchChange={setSearch}
        onStatusChange={status => setParams(previous => ({ ...previous, status, pageIndex: 1 }))}
        onReset={resetFilters} />

      <section className="swim-class-list-panel" aria-label="Danh sách lớp dạy bơi" aria-busy={list.isFetching}>
        <div className="swim-class-list-panel-heading">
          <h2>Danh sách lớp {list.data && !list.isError && <span className="swim-class-list-count">{list.data.totalCount.toLocaleString('vi-VN')}</span>}</h2>
          <Button type="text" icon={<ReloadOutlined spin={list.isFetching} />} disabled={list.isFetching} onClick={() => list.refetch()}>Làm mới</Button>
        </div>
        {list.isError ? <Alert type="error" showIcon title={list.error?.response?.status === 403 ? 'Bạn không có quyền xem danh sách lớp.' : 'Không thể tải danh sách lớp.'} action={<Button onClick={() => list.refetch()}>Thử lại</Button>} />
          : <>
            <SwimClassListTable items={list.data?.items || []} loading={list.isFetching} filtered={filtered}
              onView={classId => navigate(role === 'CUSTOMER' ? `/lop-hoc/${classId}` : `/admin/classes/${classId}`)} />
            <div className="swim-class-list-pagination">
              <Pagination current={params.pageIndex} pageSize={params.pageSize} total={list.data?.totalCount || 0} showSizeChanger pageSizeOptions={[10, 20, 50, 100]}
                showTotal={total => `${total.toLocaleString('vi-VN')} lớp`}
                onChange={(pageIndex, pageSize) => setParams(previous => ({ ...previous, pageIndex: pageSize === previous.pageSize ? pageIndex : 1, pageSize }))} />
            </div>
          </>}
      </section>
    </div>
  </ConfigProvider>
}
