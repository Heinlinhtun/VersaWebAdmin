import axios from "axios";

const api = axios.create({
        baseURL: "https://api.versapos.info/api", // Backend Port ကို ပြန်စစ်ပါ
        //baseURL: "https://localhost:7144/api", // Local Backend Port ကို ပြန်စစ်ပါ
});

// Request တိုင်းမှာ Token ပါသွားအောင် လုပ်ခြင်း
api.interceptors.request.use((config) => {
    const token = localStorage.getItem("token");
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

// Add a Response Interceptor to handle dirty tokens
api.interceptors.response.use(
    (response) => {
        return response;
    },
    (error) => {
        // Only authentication failures should force logout.
        // A normal server error must not remove the user's token.
        if (
            error.response &&
            error.response.status === 401
        ) {
            console.warn("Invalid or expired token detected. Forcing logout...");

            // Clear the bad token
            localStorage.removeItem("token");

            // Redirect to login page
            window.location.href = "/login";
        }
        return Promise.reject(error);
    },
);

export default api;
