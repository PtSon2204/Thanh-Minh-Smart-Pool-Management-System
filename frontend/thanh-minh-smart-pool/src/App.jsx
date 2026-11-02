import { ConfigProvider } from 'antd'
import { RouterProvider } from 'react-router-dom'
import router from './routes/router'
import CoachRoleSync from './features/auth/components/CoachRoleSync'

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
      <CoachRoleSync />
      <RouterProvider router={router} />
    </ConfigProvider>
  );
}
