import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

// https://vite.dev/config/
export default defineConfig({
  base: './',
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  build: {
    // WebView2-Specific Optimizations
    sourcemap: false,        // Don't generate .map files - saves 50%+ size
    cssCodeSplit: false,     // Single CSS file - faster for local WebView2
    minify: 'terser',        // Use terser for better minification
    terserOptions: {
      compress: {
        drop_console: true,  // Remove console.log in production
        drop_debugger: true  // Remove debugger statements
      }
    },
    rollupOptions: {
      output: {
        // Ensure consistent chunk naming for caching
        chunkFileNames: 'assets/[name]-[hash].js',
        entryFileNames: 'assets/[name]-[hash].js',
        assetFileNames: 'assets/[name]-[hash].[ext]'
      }
    }
  }
})
