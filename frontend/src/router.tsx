import { createBrowserRouter, Navigate } from 'react-router-dom';
import { Layout } from './components/Layout';
import { ProtectedRoute } from './auth/ProtectedRoute';
import Login from './pages/Login';
import Register from './pages/Register';
import Dashboard from './pages/Dashboard';
import Profilo from './pages/Profilo';
import Documenti from './pages/Documenti';
import Atti from './pages/Atti';
import AttoEditor from './pages/AttoEditor';
import Archivio from './pages/Archivio';
import Sedute from './pages/Sedute';
import SedutaDettaglio from './pages/SedutaDettaglio';

export const router = createBrowserRouter([
  { path: '/login', element: <Login /> },
  { path: '/register', element: <Register /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <Layout />,
        children: [
          { path: '/', element: <Dashboard /> },
          { path: '/profilo', element: <Profilo /> },
          { path: '/documenti', element: <Documenti /> },
          { path: '/atti', element: <Atti /> },
          { path: '/atti/:id', element: <AttoEditor /> },
          { path: '/archivio', element: <Archivio /> },
          { path: '/sedute', element: <Sedute /> },
          { path: '/sedute/:id', element: <SedutaDettaglio /> },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]);
