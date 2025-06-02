import { useState } from "react";

function App() {
  const [mode, setMode] = useState("login");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [email, setEmail] = useState("");
  const [error, setError] = useState("");

  const switchMode = () => {
    setMessage("");
    setUsername("");
    setPassword("");
    setConfirmPassword("");
    setMode(mode === "login" ? "register" : "login");
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    if (mode === "register") {
      if (!email || !username || !password || !confirmPassword) {
        setMessage("Lütfen tüm alanları doldurun.");
        return;
      }
    } else {
      if (!email || !password) {
        setMessage("Lütfen tüm alanları doldurun.");
        return;
      }
    }

    if (password.length < 6) {
      setMessage("Şifre en az 6 karakter olmalı.");
      return;
    }
    if (mode === "register" && password !== confirmPassword) {
      setMessage("Şifreler eşleşmiyor.");
      return;
    }

    setMessage("");
    setLoading(true);
    setTimeout(() => {
      setLoading(false);

      if (mode === "login") {
        if (email === "admin@ornek.com" && password === "123456") {
          setMessage(`Giriş başarılı! Hoşgeldin ${email}.`);
        } else {
          setMessage("Email veya şifre yanlış.");
        }
      } else {
        setMessage("Kayıt başarılı! Şimdi giriş yapabilirsiniz.");
        setMode("login");
        setUsername("");
        setPassword("");
        setConfirmPassword("");
      }
    }, 1500);
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-r from-blue-400 to-purple-600 p-6">
      <form
        onSubmit={handleSubmit}
        className="bg-white rounded-lg shadow-lg p-8 max-w-md w-full"
      >
        <h2 className="text-2xl font-bold mb-6 text-center text-gray-800">
          {mode === "login" ? "Giriş Yap" : "Kayıt Ol"}
        </h2>

        {error && (
          <div className="bg-red-100 text-red-700 p-3 rounded mb-4 text-center">
            {error}
          </div>
        )}

        <label className="block mb-2 font-semibold text-gray-700">Email</label>
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="email@ornek.com"
          className="w-full p-3 border border-gray-300 rounded mb-4 focus:outline-none focus:ring-2 focus:ring-blue-400"
          disabled={loading}
          required
        />
        {mode === "register" && (
          <>
            <label className="block mb-2 font-semibold">Kullanıcı Adı</label>
            <input
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="Kullanıcı adınızı girin"
              className="w-full p-2 border rounded mb-4 focus:outline-none focus:ring-2 focus:ring-blue-400"
              disabled={loading}
              required
            />
          </>
        )}
        <label className="block mb-2 font-semibold text-gray-700">Şifre</label>
        <input
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="Şifrenizi girin"
          className="w-full p-3 border border-gray-300 rounded mb-6 focus:outline-none focus:ring-2 focus:ring-blue-400"
          required
        />
        {mode === "register" && (
          <>
            <label className="block mb-2 font-semibold">Şifre Tekrar</label>
            <input
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              placeholder="Şifrenizi tekrar girin"
              className="w-full p-2 border rounded mb-4 focus:outline-none focus:ring-2 focus:ring-blue-400"
              disabled={loading}
              required={mode === "register"}
            />
          </>
        )}

        <button
          type="submit"
          disabled={loading}
          className="w-full bg-blue-600 text-white p-3 rounded hover:bg-blue-700 transition"
        >
          {mode === "login" ? "Giriş Yap" : "Kayıt Ol"}
        </button>

        {message && (
          <div
            className={` mt-4 mb-6 p-3 rounded text-center ${
              message.toLowerCase().includes("başarılı")
                ? "bg-green-100 text-green-700"
                : "bg-red-100 text-red-700"
            }`}
          >
            {message}
          </div>
        )}

        <p className="mt-4 text-center text-gray-600">
          {mode === "login" ? (
            <>
              Hesabın yok mu?{" "}
              <button
                type="button"
                onClick={switchMode}
                className="text-blue-600 hover:underline"
                disabled={loading}
              >
                Kayıt Ol
              </button>
            </>
          ) : (
            <>
              Zaten hesabın var mı?{" "}
              <button
                type="button"
                onClick={switchMode}
                className="text-blue-600 hover:underline"
                disabled={loading}
              >
                Giriş Yap
              </button>
            </>
          )}
        </p>
      </form>
    </div>
  );
}

export default App;
