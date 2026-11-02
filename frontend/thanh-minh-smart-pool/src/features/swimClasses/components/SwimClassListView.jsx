import { useEffect, useState } from 'react'
import { Alert, Button, ConfigProvider, Pagination } from 'antd'
import { CompassOutlined, PlusOutlined, ReloadOutlined, ScheduleOutlined } from '@ant-design/icons'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../../auth/store/authStore'
import { useSwimClasses } from '../hooks/useSwimClasses'
import SwimClassListFilters from './SwimClassListFilters'
import SwimClassListTable from './SwimClassListTable'
import '../styles/swimClassList.css'

export default function SwimClassListView() {
  const session = useAuthStore(state => state.session)
  const navigate = useNavigate()
  const location = useLocation()
  const roles = (session?.roles?.length ? session.roles : [session?.role])
    .filter(Boolean).map(item => item.toUpperCase())
  const role = location.pathname.startsWith('/coach/classes') && roles.includes('COACH')
    ? 'COACH' : session?.role?.toUpperCase()
  const [search, setSearch] = useState('')
  const [params, setParams] = useState({ searchTerm: '', status: undefined, pageIndex: 1, pageSize: 10 })
  const list = useSwimClasses({ ...params, scope: role === 'COACH' ? 'coach' : undefined }, session?.id)
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

  const title = role === 'COACH' ? 'Lớp dạy của tôi' : 'Lớp dạy bơi'
  const intro = role === 'COACH' ? 'Tạo lớp, sắp lịch và theo dõi các lớp bạn phụ trách.'
    : 'Khám phá các lớp học bơi đang hoạt động.'

  return <ConfigProvider theme={{ token: { colorPrimary: '#0077b6', colorInfo: '#0077b6', borderRadius: 8 } }}>
    <div className={`swim-class-list-page${role === 'CUSTOMER' ? ' swim-class-list-page--customer' : ' swim-class-list-page--coach'}`}>
      <header className="swim-class-list-header">
        <div className="swim-class-list-heading"><span className="swim-class-list-icon"><CompassOutlined /></span><div>
          <small>{role === 'COACH' ? 'KHU VỰC COACH' : 'KHÁM PHÁ LỚP HỌC'}</small>
          <h1>{title}</h1><p>{intro}</p>
        </div></div>
        {role === 'COACH' && <Button type="primary" size="large" icon={<PlusOutlined />} onClick={() => navigate('/coach/classes/create')}>Tạo lớp</Button>}
        {role === 'CUSTOMER' && <Button icon={<ScheduleOutlined />} onClick={() => navigate('/lop-hoc-cua-toi')}>Lớp học của tôi</Button>}
      </header>

      <SwimClassListFilters search={search} status={params.status} showStatus={role === 'COACH'}
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
              onView={classId => navigate(role === 'COACH' ? `/coach/classes/${classId}` : `/lop-hoc/${classId}`)} />
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
