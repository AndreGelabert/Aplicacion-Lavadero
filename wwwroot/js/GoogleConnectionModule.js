import { initializeApp } from 'https://www.gstatic.com/firebasejs/9.19.1/firebase-app.js';
import { getAuth, signInWithPopup, GoogleAuthProvider } from 'https://www.gstatic.com/firebasejs/9.19.1/firebase-auth.js';

const firebaseConfig = {
    apiKey: "AIzaSyBubyUIDmvFmRIvQ--pvnw9wnQcAulJJy8",
    authDomain: "aplicacion-lavadero.firebaseapp.com",
    projectId: "aplicacion-lavadero",
    storageBucket: "aplicacion-lavadero.firebasestorage.app",
    messagingSenderId: "587422469290",
    appId: "1:587422469290:web:4a11be624229aa286614d3",
    measurementId: "G-E7YCZ8WG1H"
};

const app = initializeApp(firebaseConfig);
const provider = new GoogleAuthProvider();
const auth = getAuth();

document.getElementById('google-login-button').addEventListener('click', async () => {
    try {
        const result = await signInWithPopup(auth, provider);
        const idToken = await result.user.getIdToken();
        const response = await fetch('/Login/LoginWithGoogle', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ idToken })
        });
        if (response.ok) {
            const data = await response.json();
            if (data.redirectUrl) {
                window.location.href = data.redirectUrl;
            } else {
                console.error('No se recibió la URL de redirección');
            }
        } else {
            const errorData = await response.json();
            alert(errorData.error || 'Error al iniciar sesión con Google');
        }
    } catch (error) {
        console.error('Error al iniciar sesión con Google', error);
    }
});

