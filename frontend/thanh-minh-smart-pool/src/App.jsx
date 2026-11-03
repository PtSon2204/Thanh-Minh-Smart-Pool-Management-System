import { Component } from 'react'
import { ConfigProvider, Button, Result } from 'antd'
import { RouterProvider } from 'react-router-dom'
import router from './routes/router'
import CoachRoleSync from './features/auth/components/CoachRoleSync'

class RootErrorBoundary extends Component {
  constructor(props) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error }
  }

  componentDidCatch(error, errorInfo) {
    console.error('[RootErrorBoundary] Caught React render error:', error, errorInfo)
  }

  handleReset = () => {
    try {
      sessionStorage.clear()
      localStorage.clear()
    } catch {
      // ignore
    }
    window.location.href = '/'
  }

  render() {
    if (this.state.hasError) {
      return (
        <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#f8fafc', padding: 24 }}>
          <div style={{ maxWidth: 700, width: '100%', background: '#fff', padding: 32, borderRadius: 12, boxShadow: '0 4px 20px rgba(0,0,0,0.08)', border: '1px solid #fee2e2' }}>
            <Result
              status="error"
              title="Gặp sự cố khi hiển thị giao diện"
              subTitle={this.state.error?.message || 'Đã xảy ra lỗi không xác định trong quá trình tải thành phần giao diện.'}
              extra={[
                <Button type="primary" danger key="reset" onClick={this.handleReset}>
                  Xóa bộ nhớ phiên & Tải lại trang chủ
                </Button>,
                <Button key="reload" onClick={() => window.location.reload()}>
                  Tải lại trang (F5)
                </Button>
              ]}
            >
              {this.state.error?.stack && (
                <pre style={{ background: '#f1f5f9', padding: 12, borderRadius: 6, fontSize: 12, overflowX: 'auto', maxHeight: 250, color: '#dc2626' }}>
                  {this.state.error.stack}
                </pre>
              )}
            </Result>
          </div>
        </div>
      )
    }
    return this.props.children
  }
}

export default function App() {
  return (
    <ConfigProvider
      theme={{
        token: {
          fontFamily:
            "'Open Sans', system-ui, -apple-system, Roboto, sans-serif",
        },
      }}
    >
      <RootErrorBoundary>
        <CoachRoleSync />
        <RouterProvider router={router} />
      </RootErrorBoundary>
    </ConfigProvider>
  )
}
