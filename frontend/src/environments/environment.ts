// Keep local/LAN development on port 5220. Production serves the API behind the
// same HTTPS origin through the reverse proxy, avoiding mixed-content requests.
const apiHost = typeof window === 'undefined' ? 'localhost' : window.location.hostname;
const isHttps = typeof window !== 'undefined' && window.location.protocol === 'https:';

export const environment = {
  apiUrl: isHttps ? window.location.origin : `http://${apiHost}:5220`,
};
