import { ConfigProvider } from 'antd'
import { RouterProvider } from 'react-router-dom'
import router from './routes/router'

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
      <RouterProvider router={router} />
    </ConfigProvider>
  );
}
