"""Regression tests for ByteTrack identity continuity; no model inference or alert writes."""
import unittest
from types import SimpleNamespace

import numpy as np
from ultralytics.engine.results import Boxes
from worker import CameraTracker


def tracker():
    return CameraTracker(SimpleNamespace(track_high_thresh=.25, track_low_thresh=.1,
        new_track_thresh=.25, track_buffer=30, match_thresh=.8, fuse_score=True))


def observe(tracking, rows):
    return tracking.update(Boxes(np.asarray(rows, dtype=np.float32).reshape(-1, 6), (120, 160)).numpy())


class TrackingContinuityTests(unittest.TestCase):
    def test_output_box_is_current_measurement_while_id_remains_stable(self):
        tracking = tracker()
        original = int(observe(tracking, [[10, 10, 90, 90, .9, 0]])[0, 4])
        for x in (20, 30, 40):
            result = observe(tracking, [[x, 10, x + 80, 90, .9, 0]])
            self.assertEqual(original, int(result[0, 4]))
            np.testing.assert_allclose(result[0, :4], [x, 10, x + 80, 90], atol=1e-5)

    def test_nested_duplicate_does_not_spawn_id_then_replace_body_track(self):
        tracking = tracker()
        body = [10, 10, 90, 90, .9, 0]
        original = int(observe(tracking, [body])[0, 4])
        for _ in range(5):
            result = observe(tracking, [body, [30, 30, 67, 67, .4, 0]])
            self.assertEqual([original], result[:, 4].astype(int).tolist())
        for _ in range(5):
            result = observe(tracking, [[30, 30, 67, 67, .65, 0]])
            self.assertEqual([original], result[:, 4].astype(int).tolist())

    def test_short_gap_relinks_unique_nested_observation_without_inventing_missing_boxes(self):
        tracking = tracker()
        original = int(observe(tracking, [[10, 10, 90, 90, .9, 0]])[0, 4])
        self.assertEqual(0, len(observe(tracking, [])))
        result = observe(tracking, [[30, 30, 67, 67, .65, 1]])
        self.assertEqual(original, int(result[0, 4]))
        self.assertEqual(1, int(result[0, 6]))  # Normal/drowning đổi nhãn không phải đổi người.

    def test_long_gap_does_not_use_geometry_fallback_to_transfer_old_identity(self):
        tracking = tracker()
        original = int(observe(tracking, [[10, 10, 90, 90, .9, 0]])[0, 4])
        for _ in range(5):
            self.assertEqual(0, len(observe(tracking, [])))
        observe(tracking, [[30, 30, 67, 67, .65, 0]])
        result = observe(tracking, [[30, 30, 67, 67, .65, 0]])
        self.assertNotEqual(original, int(result[0, 4]))

    def test_ambiguous_nested_observation_does_not_choose_between_two_existing_people(self):
        tracking = tracker()
        result = observe(tracking, [[10, 10, 90, 90, .9, 0], [20, 10, 100, 90, .9, 0]])
        originals = set(result[:, 4].astype(int))
        self.assertEqual(2, len(originals))
        observe(tracking, [[35, 30, 72, 67, .65, 0]])
        result = observe(tracking, [[35, 30, 72, 67, .65, 0]])
        self.assertEqual(1, len(result))
        self.assertNotIn(int(result[0, 4]), originals)

    def test_two_established_nested_tracks_are_never_merged(self):
        tracking = tracker()
        people = [[10, 10, 90, 90, .9, 0], [30, 30, 67, 67, .8, 0]]
        originals = set(observe(tracking, people)[:, 4].astype(int))
        self.assertEqual(2, len(originals))
        for _ in range(5):
            self.assertEqual(originals, set(observe(tracking, people)[:, 4].astype(int)))

    def test_another_person_nearby_can_start_a_separate_track(self):
        tracking = tracker()
        body = [10, 10, 90, 90, .9, 0]
        original = int(observe(tracking, [body])[0, 4])
        people = [body, [75, 0, 110, 35, .7, 0]]
        observe(tracking, people)
        result = observe(tracking, people)
        self.assertEqual(2, len(result))
        self.assertIn(original, result[:, 4].astype(int))

    def test_higher_confidence_nested_detection_is_not_suppressed_as_duplicate(self):
        tracking = tracker()
        body = [10, 10, 90, 90, .6, 0]
        original = int(observe(tracking, [body])[0, 4])
        people = [body, [30, 30, 67, 67, .95, 0]]
        observe(tracking, people)
        result = observe(tracking, people)
        self.assertEqual(2, len(result))
        self.assertIn(original, result[:, 4].astype(int))


if __name__ == '__main__':
    unittest.main()
