import { useEffect } from 'react'
import { Link, useLocation } from 'react-router-dom'
import duckImage from '../../assets/duck-401.png'
import './Error401Page.css'

export default function Error401Page() {
  const location = useLocation()
  const customMessage = location.state?.message

  useEffect(() => {
    document.title = '401 Unauthorized | Thanh Minh Smart Pool'
  }, [])

  return (
    <div className="error-401-container">
      <div className="error-401-content">
        <h1 className="error-401-title">401 Error</h1>
        <h2 className="error-401-subtitle">Oops, something went wrong.</h2>
        <p className="error-401-desc">{customMessage || 'Invalid credentials.'}</p>

        <div className="error-401-action">
          <span>Click here to</span>
          <Link to="/dang-nhap" className="error-401-signin-link" state={{ from: location.state?.from }}>
            signin
          </Link>
        </div>

        <Link to="/" className="error-401-home-link">
          ← Quay về Trang chủ
        </Link>

        <div className="error-401-duck-wrapper">
          <img
            src={duckImage}
            alt="401 Unauthorized Duck Illustration"
            className="error-401-duck"
          />
        </div>
      </div>
    </div>
  )
}
