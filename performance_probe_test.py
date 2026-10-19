import unittest
from unittest.mock import patch
import performance_probe as probe


class PerformanceTests(unittest.TestCase):
    def test_cpu_ignores_guest_duplicate(self):
        with patch.object(probe.Path, 'read_text', return_value='cpu 100 0 40 800 20 10 20 10 50 5\n'):
            self.assertEqual(probe.cpu_counters(), (1000, 820))

    def test_idle_busy_and_no_sample(self):
        self.assertEqual(probe.cpu_percent((100, 80), (200, 180)), 0)
        self.assertEqual(probe.cpu_percent((100, 80), (200, 80)), 100)
        self.assertIsNone(probe.cpu_percent((100, 80), (100, 80)))

    def test_network_omits_loopback(self):
        sample = 'head\nhead\n lo: 900 0 0 0 0 0 0 0 800 0 0 0 0 0 0 0\n eth0: 100 0 0 0 0 0 0 0 200 0 0 0 0 0 0 0\n'
        with patch.object(probe.Path, 'read_text', return_value=sample):
            self.assertEqual(probe.network_counters(), (100, 200))


if __name__ == '__main__':
    unittest.main()
