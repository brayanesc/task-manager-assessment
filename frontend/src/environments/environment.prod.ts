// In Docker, nginx proxies /api/* to the backend container.
// Using a relative URL means the JS bundle works on any hostname/port.
export const environment = {
  apiUrl: '/api',
};
