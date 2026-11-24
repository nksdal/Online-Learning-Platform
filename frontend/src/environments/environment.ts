// Use the host that served the frontend so a phone on the same Wi-Fi calls the PC's API,
// rather than trying to reach localhost on the phone itself.
const apiHost = typeof window === 'undefined' ? 'localhost' : window.location.hostname;

export const environment = {
  apiUrl: `http://${apiHost}:5220`,
};
